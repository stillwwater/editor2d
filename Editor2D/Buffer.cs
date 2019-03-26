using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor2D
{
    public class Buffer
    {
        public enum Mode
        {
            Normal,
            Grab,
            Scale,
            Box,
            Palette,
            Camera
        }

        internal readonly GameObject[] palette;

        internal Chunk chunk;
        internal Mode mode;
        internal int layer;
        internal int palette_index;
        internal Cursors cursors;
        internal string log;
        internal readonly Undo undo;

        int entityid;
        Camera view;
        GameObject[] selection = new GameObject[16];
        List<GameObject> deletion_pool = new List<GameObject>();
        Vector4 selection_rect;

        internal Buffer(Chunk chunk, GameObject[] palette, Camera view) {
            this.view    = view;
            this.chunk   = chunk;
            this.palette = palette;
            undo = new Undo();
            cursors = new Cursors(chunk.bounds, chunk.cell_scale);
        }

        internal void Free() {
            foreach (var entity in deletion_pool) {
                GameObject.Destroy(entity);
            }

            Array.Resize(ref selection, 1);
            DeselectAll();
            undo.Clear();
        }

        internal void SwitchMode(Mode new_mode) {
            switch (new_mode) {
                case Mode.Normal:
                    if (mode == Mode.Grab)
                        GridRestoreAtCursors(cursors);
                    DeselectAll();
                    break;

                case Mode.Grab:
                case Mode.Scale:
                    cursors.Sync();
                    if (mode == new_mode) {
                        mode = Mode.Normal;
                        GridRestoreAtCursors(cursors);
                        return;
                    }

                    undo.PushFrame(layer);
                    if (!SelectAtCursors(ref selection)) {
                        undo.PopFrame(out _);
                        if (mode != Mode.Box)
                            DeselectAll();
                        return;
                    }
                    break;

                case Mode.Box:
                    if (mode == Mode.Box) {
                        mode = Mode.Normal;
                        cursors.Sync();
                        return;
                    }

                    Vector3 last = cursors[cursors.Count - 1].position;
                    selection_rect = new Vector4(last.x, last.y, last.x, last.y);
                    break;

                case Mode.Palette:
                    if (mode == Mode.Palette) {
                        mode = Mode.Normal;
                        return;
                    }
                    break;
            }
            mode = new_mode;
        }

        internal void CreateFromPalette(int index) {
            if (palette.Length == 0) {
                Debug.LogWarning("[e2d] No palette assets assigned.");
                return;
            }

            if (index >= palette.Length || index < 0) {
                Debug.LogErrorFormat("[e2d] No palette entity at {0}.", index);
                return;
            }
            mode = Mode.Normal;
            undo.PushFrame(layer);

            // @Todo: Keep prefab link

            for (int i = 0; i < cursors.Count; i++) {
                var entity = GameObject.Instantiate(palette[index]);
                entity.name = string.Format(
                    "{0}_{1:X3}",
                    palette[index].name,
                    entityid++);

                entity.transform.position = cursors[i].position;
                entity.SetActive(false);

                undo.RegisterState(entity);
                entity.SetActive(true);
                GridAssign(entity, cursors[i].position);
                // @Todo: Invoke(created, created)
            }
            GridRestoreAtCursors(cursors);
        }

        /// Find palette from which this entity was created.
        /// Uses string compare, so the child must share a
        /// common name with the parent (default for entities
        /// created using Buffer.CreateFromPalette).
        internal GameObject FindParent(GameObject entity) {
            foreach (var parent in palette) {
                if (entity.name.Contains(parent.name))
                    return parent;
            }

            return null;
        }

        internal void Erase() {
            undo.PushFrame(layer);

            for (int i = 0; i < cursors.Count; i++) {
                Vector2Int grid_pos;

                if (!MapCoordinate(cursors[i].position, out grid_pos)) {
                    continue;
                }

                var entity = Select(grid_pos);
                undo.RegisterState(entity);
                Kill(entity);
            }
        }

        internal Vector3 Move(GameObject entity, Vector3 offset) {
            GridAssign(entity, entity.transform.position + offset);
            entity.transform.position += offset;
            return entity.transform.position;
        }

        internal (Vector3, Vector3) Scale(GameObject entity, Vector3 offset) {
            Vector3 scl = (entity.transform.localScale + offset) * chunk.cell_scale;

            if (scl.x == 0) scl.x += offset.x;
            if (scl.y == 0) scl.y += offset.y;

            if (scl.x < 0 || scl.y < 0)
                log = "~-SCALE~";

            float mag = (entity.transform.localScale - scl).magnitude;
            Vector3 pos = entity.transform.position + (mag * offset) / 2f;

            entity.transform.localScale = scl;
            GridAssign(entity, pos);
            entity.transform.position = pos;
            return (pos, scl);
        }

        internal void Transform(Vector3 offset) {
            float min_x = Mathf.Infinity, max_x = Mathf.NegativeInfinity;
            float min_y = Mathf.Infinity, max_y = Mathf.NegativeInfinity;

            for (int i = 0; i < cursors.Count; i++) {
                Vector3 position = cursors[i].position + offset;
                min_x = Mathf.Min(min_x, position.x);
                max_x = Mathf.Max(max_x, position.x);
                min_y = Mathf.Min(min_y, position.y);
                max_y = Mathf.Max(max_y, position.y);

                if (!MapCoordinate(position, out _)) {
                    // Moving out of bounds
                    return;
                }
            }

            ScrollView(
                new Vector3(min_x, min_y),
                new Vector3(max_x, max_y),
                offset);

            if (mode == Mode.Box) {
                selection_rect.z += offset.x;
                selection_rect.w += offset.y;
                SelectInRect(selection_rect);
                return;
            }

            for (int i = 0; i < cursors.Count; i++) {
                if (!cursors[i].pinned || mode != Mode.Normal) {
                    cursors.SetUnchecked(i, new Cursor() {
                        position = cursors[i].position + offset,
                        pinned   = false
                    });
                }

                if (i >= selection.Length || !selection[i])
                    continue; // Empty selection

                switch (mode) {
                    case Mode.Grab:
                        Move(selection[i], offset);
                        break;

                    case Mode.Scale:
                        var (pos, scl) = Scale(selection[i], offset);
                        ClampCursor(i, pos, scl);
                        break;
                }
            }
        }

        ///
        /// Select similar entities that are next to each other
        /// under the cursor.
        ///
        internal void SelectSimilar() {
            // Use last placed cursor
            Vector3 anchor = cursors[cursors.Count - 1].position;
            var entity = Select(anchor);

            if (!entity)
                return;

            var parent = FindParent(entity);

            if (!parent) {
                Debug.LogWarningFormat("[e2d] No parent for {0}.", entity.name);
                return;
            }

            cursors.Clear();
            SelectFloodFill(anchor, parent.name);
        }

        internal void SelectInRect(Vector4 area) {
            Vector2 point_a = new Vector2(area.x, area.y);
            Vector2 point_b = new Vector2(area.z, area.w);
            cursors.Clear();

            Vector2 size = (point_b - point_a) / chunk.cell_scale;
            cursors.Reserve((int)Mathf.Abs(size.x * size.y));

            if (point_b.x < point_a.x)
                (point_b.x, point_a.x) = (point_a.x, point_b.x);

            if (point_b.y < point_a.y)
                (point_b.y, point_a.y) = (point_a.y, point_b.y);

            for (float x = point_a.x; x <= point_b.x; x += chunk.cell_scale) {
                for (float y = point_a.y; y <= point_b.y; y += chunk.cell_scale) {
                    Vector3 pos = new Vector3(x, y);
                    cursors.AddUnchecked(pos);
                }
            }
        }

        internal void SelectAllInLayer() {
            cursors.Clear();
            for (int i = 0; i < chunk.layers[layer].grid.GetLength(0); i++) {
                for (int j = 0; j < chunk.layers[layer].grid.GetLength(1); j++) {
                    if (!Select(new Vector2Int(i, j))) continue;
                    float x = (i + chunk.bounds.x) * chunk.cell_scale;
                    float y = (j + chunk.bounds.y) * chunk.cell_scale;
                    cursors.Add(new Vector3(x, y));
                }
            }
        }

        internal void DeselectAll() {
            if (cursors.Count == 0) return;
            Vector3 position = cursors[cursors.Count - 1].position;
            cursors.Clear();
            cursors.Add(new Cursor() {
                position = position,
                pinned   = false
            });
        }

        internal void PinCursor() {
            if (cursors.Count == 0) {
                Debug.LogError("[e2d] No cursors to pin.");
                return;
            }

            var last = cursors[cursors.Count - 1];

            for (int i = 0; i < cursors.Count - 1; i++) {
                // Don't pin mutiple cursors in the same location
                if (cursors[i].position == last.position)
                    return;
            }

            cursors.Add(last);
            cursors[cursors.Count - 2] = new Cursor() {
                position = last.position, pinned = true
            };
        }

        /// Select entity at a grid position
        internal GameObject Select(Vector2Int grid_pos) {
            var temp   = chunk.layers[layer].temp[grid_pos.x, grid_pos.y];
            var entity = chunk.layers[layer].grid[grid_pos.x, grid_pos.y];

            // Temp is checked first
            entity = temp && temp.activeSelf ? temp : entity;

            if (entity)
                // Inactive entities can't be selected
                return entity.activeSelf ? entity : null;

            return entity;
        }

        /// Select entity at a world position
        internal GameObject Select(Vector3 position) {
            if (MapCoordinate(position, out Vector2Int grid_pos)) {
                return Select(grid_pos);
            }
            return null;
        }

        /// Map vector position to index on grid
        internal bool MapCoordinate(Vector3 position, out Vector2Int index) {
            int x = (int)((position.x - chunk.bounds.x) / chunk.cell_scale);
            int y = (int)((position.y - chunk.bounds.y) / chunk.cell_scale);

            int scaled_w = chunk.ScaledBounds.width;
            int scaled_h = chunk.ScaledBounds.height;

            if (x < 0 || y < 0 || x > scaled_w || y > scaled_h) {
                // Out of bounds
                // @Todo: Allocate bigger grid/ new chunk when out of bounds?
                index = Vector2Int.zero;
                return false;
            }
            index = new Vector2Int(x, y);
            return true;
        }

        internal void Revert(Undo.Frame frame) {
            mode = Mode.Normal;
            layer = frame.layer;

            foreach (var state in frame.states) {
                if (state.entity.activeSelf && !state.alive) {
                    Kill(state.entity);
                    continue;
                }

                if (!state.entity.activeSelf && state.alive) {
                    Revive(state.entity);
                    continue;
                }

                GridAssign(state.entity, state.position);
                state.entity.transform.position = state.position;
                state.entity.transform.localScale = state.scale;
                state.entity.transform.rotation = state.rotation;
            }

            // Give some info on how many actions were undone and how old
            // the changes are.
            string time = undo.TimeDelta(frame);
            string s = frame.states.Count == 1 ? "" : "s";
            log = string.Format("{0} change{1}; {2}", frame.states.Count, s, time);
        }

        internal void FocusAtCursors() {
            float min_x = Mathf.Infinity, max_x = Mathf.NegativeInfinity;
            float min_y = Mathf.Infinity, max_y = Mathf.NegativeInfinity;

            // Find selection area
            for (int i = 0; i < cursors.Count; i++) {
                Vector3 position = cursors[i].position;
                min_x = Mathf.Min(min_x, position.x);
                max_x = Mathf.Max(max_x, position.x);
                min_y = Mathf.Min(min_y, position.y);
                max_y = Mathf.Max(max_y, position.y);
            }

            Vector2 min = new Vector2(min_x, min_y);
            Vector2 max = new Vector2(max_x, max_y);
            Vector2 mid = (min + max) / 2;

            // Focus view on the center of the selected region
            float z = view.transform.position.z;
            view.transform.position = new Vector3(mid.x, mid.y, z);
        }

        /// Position cursor in the corner of a scaled entity
        void ClampCursor(int cursor_index, Vector3 position, Vector3 scale) {
            Vector2 dir = Vector2.zero;
            dir.x = Mathf.Clamp(Mathf.RoundToInt(scale.x), -1, 1);
            dir.y = Mathf.Clamp(Mathf.RoundToInt(scale.y), -1, 1);

            float half = chunk.cell_scale / 2;
            Vector3 offset = new Vector3(half*dir.x, half*dir.y);

            cursors[cursor_index] = new Cursor() {
                position = position + (scale / 2f) - offset,
                pinned   = false
            };
        }

        bool SelectAtCursors(ref GameObject[] selection) {
            if (selection.Length < cursors.Count) {
                // Reallocate selection buffer
                Array.Resize(ref selection, cursors.Count);
            }

            bool has_selection = false;

            for (int i = 0; i < cursors.Count; i++) {
                Vector3 position = cursors[i].position;

                // Remove overlapping cursors
                if (cursors.IsDuplicate(position))
                    cursors.RemoveDuplicate(position, i);

                selection[i] = Select(position);

                if (selection[i]) {
                    undo.RegisterState(selection[i]);
                    has_selection = true;
                }
            }

            return has_selection;
        }

        void SelectFloodFill(Vector3 anchor, string match) {
            var nodes = new Stack<Vector3>();
            nodes.Push(anchor);

            while (nodes.Count > 0) {
                anchor = nodes.Pop();
                var entity = Select(anchor);

                if (!entity || !entity.name.Contains(match))
                    continue;

                if (cursors.Contains(anchor))
                    // Already visited
                    continue;

                cursors.Add(anchor);
                // @Todo: Scale
                nodes.Push(anchor + Vector3.up);
                nodes.Push(anchor + Vector3.right);
                nodes.Push(anchor + Vector3.down);
                nodes.Push(anchor + Vector3.left);
            }
        }

        void Kill(GameObject entity) {
            if (!entity) return;
            entity.SetActive(false);
            deletion_pool.Add(entity);
        }

        void Revive(GameObject entity) {
            entity.SetActive(true);
            deletion_pool.Remove(entity);
            var other = Select(entity.transform.position);
            if (other && other != entity) {
                // Can't have two entities occupy the same space in the
                // same layer.
                Kill(other);
            }
            GridAssign(entity, entity.transform.position);
        }

        /// Add reference to an entity at a position in the grid
        void GridAssign(GameObject e, Vector3 position) {
            Vector2Int grid_pos;
            if (!MapCoordinate(position, out grid_pos)) return;

            Vector2Int old_pos;
            if (!MapCoordinate(e.transform.position, out old_pos)) return;

            var entity = chunk.layers[layer].grid[grid_pos.x, grid_pos.y];

            if (chunk.layers[layer].temp[old_pos.x, old_pos.y] == e) {
                chunk.layers[layer].temp[old_pos.x, old_pos.y] = null;
            }

            if (chunk.layers[layer].grid[old_pos.x, old_pos.y] == e) {
                chunk.layers[layer].grid[old_pos.x, old_pos.y] = null;
            }

            if (entity && entity != e) {
                // Entities cannot be assigned to the same node,
                // must be moved to temporary slot to be cleaned later.
                chunk.layers[layer].temp[grid_pos.x, grid_pos.y] = e;
                return;
            }

            chunk.layers[layer].grid[grid_pos.x, grid_pos.y] = e;
        }

        ///
        /// Delete duplicate entities and move entities from
        /// temp to grid.
        ///
        void GridRestore(Vector3 position) {
            Vector2Int grid_pos;
            if (!MapCoordinate(position, out grid_pos)) {
                return;
            }

            var entity = chunk.layers[layer].grid[grid_pos.x, grid_pos.y];
            var temp   = chunk.layers[layer].temp[grid_pos.x, grid_pos.y];

            if (entity && temp) {
                undo.RegisterState(entity);

                // Multiple entities in one tile, entity must be destroyed.
                Kill(entity);
                chunk.layers[layer].grid[grid_pos.x, grid_pos.y] = temp;
                chunk.layers[layer].temp[grid_pos.x, grid_pos.y] = null;
                return;
            }

            if (!entity && temp) {
                chunk.layers[layer].grid[grid_pos.x, grid_pos.y] = temp;
                chunk.layers[layer].temp[grid_pos.x, grid_pos.y] = null;
                return;
            }
        }

        void GridRestoreAtCursors(Cursors cursors) {
            for (int i = 0; i < cursors.Count; i++) {
                GridRestore(cursors[i].position);
            }
        }

        ///
        /// Determine if an area described by its bottom-left
        /// and top-right coordinates is visible to the camera.
        /// If not visible, scroll the view to focus on the area.
        ///
        void ScrollView(Vector3 min, Vector3 max, Vector3 offset) {
            Vector3 min_vis = view.WorldToViewportPoint(min);
            Vector3 max_vis = view.WorldToViewportPoint(max);

            if ((min_vis.x >= 0 && min_vis.x <= 1)
                && (min_vis.y >= 0 && min_vis.y <= 1)
                && (max_vis.x >= 0 && max_vis.x <= 1)
                && (max_vis.y >= 0 && max_vis.y <= 1)) {
                // Area within camera viewport
                return;
            }

            view.transform.position += offset;
        }
    }
}
