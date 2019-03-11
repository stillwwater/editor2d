using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor2D
{
    internal struct Cursor
    {
        internal Vector3 position;
        internal bool pinned;
    }

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
        internal List<Cursor> cursors;
        internal string log;
        internal readonly Undo undo;

        int entityid;
        Camera view;
        GameObject[] selection = new GameObject[16];
        List<GameObject> deletion_pool = new List<GameObject>();

        internal Buffer(Chunk chunk, GameObject[] palette, Camera view) {
            this.view = view;
            this.chunk = chunk;
            this.palette = palette;
            undo = new Undo();
            cursors = new List<Cursor>(1);
            cursors.Add(new Cursor() { position = Vector3.zero, pinned = false } );
        }

        internal void Free() {
            foreach (var entity in deletion_pool) {
                GameObject.Destroy(entity);
            }

            Array.Resize(ref selection, 1);
            DeselectAll();
        }

        internal void SwitchMode(Mode new_mode) {
            switch (new_mode) {
                case Mode.Normal:
                    DeselectAll();
                    break;

                case Mode.Grab:
                    if (mode == Mode.Grab) {
                        mode = Mode.Normal;
                        GridRestoreAtCursors(cursors);
                        return;
                    }

                    undo.PushFrame(layer);
                    if (!SelectAtCursors(ref selection)) {
                        undo.PopFrame(out _);
                        return;
                    }
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
            if (index >= palette.Length || index < 0) {
                Debug.LogErrorFormat("[e2d]: No palette entity at {0}", index);
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

        internal void Move(GameObject entity, Vector3 offset) {
            GridAssign(entity, entity.transform.position + offset);
            entity.transform.position += offset;
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


            for (int i = 0; i < cursors.Count; i++) {
                if (!cursors[i].pinned || mode != Mode.Normal) {
                    cursors[i] = new Cursor() {
                        position = cursors[i].position + offset,
                        pinned = false
                    };
                }

                if (!selection[i] || i >= selection.Length)
                    continue; // Empty selection

                switch (mode) {
                    case Mode.Grab: Move(selection[i], offset); break;
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
                    cursors.Add(new Cursor() {
                        position = new Vector3(x, y),
                        pinned = false
                    });
                }
            }
        }

        internal void DeselectAll() {
            if (cursors.Count == 0) return;
            cursors[0] = cursors[cursors.Count - 1];
            cursors.RemoveRange(0, cursors.Count - 1);
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
            cursors[cursors.Count - 2] = new Cursor() { position = last.position, pinned = true };
        }

        /// Select entity at a grid position
        internal GameObject Select(Vector2Int grid_pos) {
            var temp   = chunk.layers[layer].temp[grid_pos.x, grid_pos.y];
            var entity = chunk.layers[layer].grid[grid_pos.x, grid_pos.y];

            // Temp is checked first
            entity = temp ? temp : entity;

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

            float z = view.transform.position.z;
            view.transform.position = new Vector3(mid.x, mid.y, z);
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
                for (int j = 0; j < cursors.Count; j++) {
                    if (j != i && position == cursors[j].position) {
                        cursors.RemoveAt(j);
                        continue;
                    }
                }
                selection[i] = Select(position);

                if (selection[i]) {
                    undo.RegisterState(selection[i]);
                    has_selection = true;
                }
            }

            return has_selection;
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

        void GridRestoreAtCursors(List<Cursor> cursors) {
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
