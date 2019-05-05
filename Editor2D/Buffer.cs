using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Lvl2D;

namespace Editor2D
{
    public class Buffer
    {
        public enum Mode
        {
            Normal,
            Grab,
            Box,
            Line,
            Palette,
            Camera
        }

        public struct Config
        {
            public string name;
            public string path;
            public bool set_sorting_order;
            public bool set_camera_color;
        }

        internal readonly GameObject[] palette;
        internal readonly Cursors cursors;
        internal readonly Undo undo;
        internal Mode mode;
        internal int layer;
        internal int palette_index;
        internal string log;
        internal Chunk chunk;

        readonly GameObject[] prefab_parents;
        readonly List<GameObject> deletion_pool;
        readonly Camera view;
        readonly Config config;

        int entityid;
        Vector4 selection_rect;
        int line_origin;
        GameObject[] selection;

        internal Buffer(Chunk chunk, GameObject[] palette, Camera view, Config config) {
            this.view    = view;
            this.chunk   = chunk;
            this.palette = palette;
            this.config  = config;
            undo = new Undo();
            cursors = new Cursors(chunk.bounds, chunk.cell_scale);
            deletion_pool = new List<GameObject>();
            selection = new GameObject[16];
            prefab_parents = new GameObject[palette.Length];
        }

        internal void Free() {
            foreach (var entity in deletion_pool) {
                GameObject.Destroy(entity);
            }

            Array.Resize(ref selection, 1);
            DeselectAll();
            undo.Clear();
        }

        internal Mode SwitchMode(Mode new_mode) {
            switch (new_mode) {
                case Mode.Normal:
                    if (mode == Mode.Grab)
                        GridRestoreAtCursors(cursors);
                    DeselectAll();
                    mode = new_mode;
                    break;

                case Mode.Grab:
                    ToggleTransformMode(new_mode);
                    break;

                case Mode.Box:
                    ToggleBoxMode(new_mode);
                    break;

                case Mode.Line:
                    ToggleLineMode(new_mode);
                    break;

                case Mode.Palette:
                    if (mode == Mode.Palette) {
                        mode = Mode.Normal;
                        break;
                    }
                    mode = new_mode;
                    break;
            }
            return mode;
        }

        internal void CreateFromPalette(int index) {
            if (palette.Length == 0) {
                Debug.LogWarning("[e2d] No palette assets assigned.");
                return;
            }

            if (index >= palette.Length || index < 0) {
                Debug.LogErrorFormat("[e2d] No palette entry at {0}.", index);
                return;
            }
            mode = Mode.Normal;
            undo.PushFrame(layer);
            cursors.Sync();

            for (int i = 0; i < cursors.Count; i++) {
                Vector3 position = cursors[i].position;

                if (cursors.IsDuplicate(position))
                    cursors.RemoveDuplicate(position, i);

                var entity = CreateEntity(palette[index], palette[index].name, position);
                var parent = ParentContainer(index);
                entity.transform.SetParent(parent.transform);
            }
            GridRestoreAtCursors(cursors);
        }

        internal void Erase() {
            undo.PushFrame(layer);

            for (int i = 0; i < cursors.Count; i++) {
                Vector2Int grid_pos;

                // @Cleanup: unnecessary
                if (!MapCoordinate(cursors[i].position, out grid_pos)) {
                    continue;
                }

                // @Todo: Null check
                var entity = Select(grid_pos);
                undo.RegisterState(entity);
                Kill(entity);
            }
        }

        internal void Clone() {
            mode = Mode.Normal;
            undo.PushFrame(layer);
            cursors.Sync();

            for (int i = 0; i < cursors.Count; i++) {
                Vector3 position = cursors[i].position;

                if (cursors.IsDuplicate(position))
                    cursors.RemoveDuplicate(position, i);

                var original = Select(position);
                if (!original) continue;

                int palette_index = FindPrefab(original, out GameObject prefab);
                Debug.Assert(palette_index >= 0);

                string base_name = prefab.name;
                var parent = ParentContainer(palette_index);
                var clone = CreateEntity(prefab, base_name, position);
                clone.transform.SetParent(parent.transform);
            }
            ToggleTransformMode(Mode.Grab);
            // Remove frame created by grab since it doesn't make sense
            // to revert the cloned entities to the position they came
            // from (a frame for the clone operation already exists).
            undo.PopFrame(out _);
        }

        ///
        /// Find palette entry from which this entity was created.
        /// Uses string compare, so the child must share a common
        /// name with the prefab (default for entities created
        /// using Buffer.CreateFromPalette).
        ///
        internal int FindPrefab(GameObject entity, out GameObject prefab) {
            for (int i = 0; i < palette.Length; i++) {
                if (entity.name.Contains(palette[i].name)) {
                    prefab = palette[i];
                    return i;
                }
            }
            prefab = null;
            return -1;
        }

        internal void Flip(Vector3 axis) {
            undo.PushFrame(layer);
            cursors.Sync();
            SelectAtCursors(ref selection);

            for (int i = 0; i < cursors.Count; i++) {
                if (i >= selection.Length || !selection[i]) continue;
                var sprite = selection[i].GetComponent<SpriteRenderer>();
                Debug.Assert(sprite);

                if (axis.x != 0)
                    sprite.flipX = !sprite.flipX;

                if (axis.y != 0)
                    sprite.flipY = !sprite.flipY;
            }

            undo.PopFrame(out _); // @Todo: undo flip action
        }

        internal void Rotate() {
            undo.PushFrame(layer);
            cursors.Sync();
            SelectAtCursors(ref selection);

            for (int i = 0; i < cursors.Count; i++) {
                if (i >= selection.Length || !selection[i]) continue;
                var entity = selection[i];
                Vector3 rot = entity.transform.localRotation.eulerAngles;

                if ((rot.z -= 90) >= 360) // Rotate clockwise
                    rot.z = 0;

                entity.transform.localRotation = Quaternion.Euler(rot);
            }
        }

        internal Vector3 Move(GameObject entity, Vector3 offset) {
            GridAssign(entity, entity.transform.position + offset);
            entity.transform.position += offset;
            return entity.transform.position;
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
                if (!cursors[i].pinned || (mode != Mode.Normal && mode != Mode.Line)) {
                    cursors.SetUnchecked(i, new Cursor() {
                        position = cursors[i].position + offset,
                        pinned   = false
                    });
                }

                if (i >= selection.Length || !selection[i])
                    continue; // Empty selection

                if (mode == Mode.Grab)
                    Move(selection[i], offset);
            }
            if (mode == Mode.Line) SelectLine(line_origin);
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

            FindPrefab(entity, out GameObject prefab);

            if (!prefab) {
                Debug.LogWarningFormat("[e2d] No palette entry for {0}.", entity.name);
                return;
            }

            cursors.Clear();
            SelectFloodFill(anchor, prefab.name);
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

        internal void SelectLine(int origin) {
            if (cursors.Count < 2 || origin >= cursors.Count)
                return;

            Vector3 point_a = cursors[origin-1].position;
            Vector3 point_b = cursors[origin].position;

            if (point_a == point_b)
                return;

            int rem = origin + 1;
            if (cursors.Count > rem)
                // Clear cursors from the origin until the end since
                // these will be modified.
                cursors.RemoveRange(rem, cursors.Count - rem, sync: false);

            DrawCursorLine(point_a, point_b);
        }

        ///
        /// Create new endpoints for the current line
        /// or create a new line.
        ///
        internal void LineCreateVertex() {
            Debug.Assert(cursors.Count > 0);
            if (mode != Mode.Line) {
                DeselectAll();
                PinCursor();
                line_origin = cursors.Count - 1;
                mode = Mode.Line;
                return;
            }

            cursors.Sync();
            // To create a new vertex we need to swap the last cursor with
            // the current origin, which will be earlier in the cursors list
            // than the cursors created for the current line.
            var start = cursors[line_origin];
            cursors[line_origin] = cursors[cursors.Count - 1];

            cursors[cursors.Count - 1] = new Cursor() {
                position = start.position,
                pinned   = true
            };
            // The new origin, point_a is now the previous origin
            // point_a is at Count - 2 (pinned)
            // point_b is at Count - 1 (line_origin)
            cursors.Add(start.position);
            line_origin = cursors.Count - 1;
        }

        internal void SelectAllInLayer() {
            Debug.Assert(cursors.Count > 0);
            var last = cursors[cursors.Count - 1];
            cursors.Clear();

            for (int i = 0; i < chunk.layers[layer].grid.GetLength(0); i++) {
                for (int j = 0; j < chunk.layers[layer].grid.GetLength(1); j++) {
                    if (!Select(new Vector2Int(i, j))) continue;
                    float x = (i + chunk.bounds.x) * chunk.cell_scale;
                    float y = (j + chunk.bounds.y) * chunk.cell_scale;
                    cursors.Add(new Vector3(x, y));
                }
            }
            if (cursors.Count == 0)
                cursors.Add(last);
        }

        internal void DeselectAll() {
            Debug.Assert(cursors.Count > 0);
            Vector3 position = cursors[cursors.Count - 1].position;
            cursors.Clear();
            cursors.Add(new Cursor() {
                position = position,
                pinned   = false
            });
        }

        internal void PinCursor() {
            Debug.Assert(cursors.Count > 0);
            cursors.Sync();
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

        internal void RemovePinnedCursor() {
            Debug.Assert(cursors.Count > 0);
            cursors.Sync();
            var last = cursors[cursors.Count - 1];
            cursors.RemoveDuplicate(last.position, cursors.Count - 1);

            if (cursors[cursors.Count - 1].position != last.position)
                cursors.Add(last.position, pinned: false);
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

            int scaled_w = chunk.scaled_bounds.width;
            int scaled_h = chunk.scaled_bounds.height;

            if (x < 0 || y < 0 || x >= scaled_w || y >= scaled_h) {
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

        internal void WriteBufferToFile() {
            const string signature = "e2d (lvl2d)";
            Color32 view_color = (Color32)view.backgroundColor;

            var header = new LvlHeader() {
                palette = (ushort)palette.Length,
                layers  = (ushort)chunk.layers.Count,
                width   = (ushort)(chunk.scaled_bounds.width),
                height  = (ushort)(chunk.scaled_bounds.height),
                name    = config.name,
                author  = signature,
                camera  = new LvlHeader.Camera() {
                    x = view.transform.position.x,
                    y = view.transform.position.y,
                    z = view.transform.position.z,
                    ortho_size = view.orthographicSize,
                    set_color = config.set_camera_color,
                    r = view_color.r,
                    g = view_color.g,
                    b = view_color.b
                }
            };

            Func<GameObject, int> find_prefab = (e) => FindPrefab(e, out _);
            Func<int, LvlLayer> find_layer = (l) => {
                return new LvlLayer() {
                    layer_id = l,
                    z_depth = chunk.layers[l].z_depth
                };
            };
            Func<int, Vector2Int, GameObject> find_entity = (l, grid_pos) => {
                layer = l;
                return Select(grid_pos);
            };

            using (var fs = File.Open(config.path, FileMode.Create)) {
                using (var bin = new BinaryWriter(fs)) {
                    var lvl = new LvlWriter(bin);
                    lvl.WriteAll(header, find_layer, find_entity, find_prefab);
                }
            }
            log = string.Format("Write: {0}", config.path);
        }

        internal void LoadBufferFromFile() {
            chunk.layers.Clear();
            Action<LvlLayer> create_layer = (l) => {
                if (l.layer_id >= chunk.layers.Count)
                    ChunkUtil.Realloc(ref chunk, 1, l.z_depth);

                // Assume layer_id as the layer index
                layer = l.layer_id;
            };

            Action<LvlEntity, Vector2Int> create_entity = (entity, grid_pos) => {
                float x = (chunk.bounds.x + grid_pos.x) * chunk.cell_scale;
                float y = (chunk.bounds.y + grid_pos.y) * chunk.cell_scale;
                Vector3 position = new Vector3(x, y, chunk.layers[layer].z_depth);
                CreateFromLvlEntity(entity, position);
            };

            using (var fs = File.OpenRead(config.path)) {
                using (var bin = new BinaryReader(fs)) {
                    var lvl = new LvlReader(bin);
                    undo.PushFrame(layer); // Need new frame for generated entities

                    if (!lvl.ReadAll(create_layer, create_entity, out LvlHeader header))
                        return;

                    view.transform.position = new Vector3(
                        header.camera.x,
                        header.camera.y,
                        header.camera.z);

                    if (config.set_camera_color) {
                        view.backgroundColor = new Color32(
                            r: header.camera.r,
                            g: header.camera.g,
                            b: header.camera.b,
                            a: 0);
                    }
                }
            }
            log = string.Format("Open: {0}", config.path);
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

        ///
        /// Draw a line using cursors between two points
        /// using Bresenham's Line Drawing algorithm.
        ///
        void DrawCursorLine(Vector3 point_a, Vector3 point_b) {
            Vector3 delta = point_b - point_a;
            Vector2 inc   = new Vector2(chunk.cell_scale, chunk.cell_scale);
            Vector2 error = Vector2.zero;

            if (delta.y < 0) {
                delta.y = -delta.y;
                inc.y = -inc.y;
            }
            if (delta.x < 0) {
                delta.x = -delta.x;
                inc.x = -inc.x;
            }

            // Stop before the end point
            point_b -= (Vector3)inc;

            if (point_a == point_b)
                return;

            if (delta.y > delta.x) {
                // for abs(dy/dx) > 1 bias y axis
                while (point_a.y != point_b.y) {
                    point_a.y += inc.y;
                    error.y += delta.x * 2;
                    if (error.y > delta.y) {
                        point_a.x += inc.x;
                        error.y -= delta.y * 2;
                    }
                    cursors.AddUnchecked(point_a, pinned: true);
                }
                return;
            }

            while (point_a.x != point_b.x) {
                point_a.x += inc.x;
                error.x += delta.y * 2;
                if (error.x > delta.x) {
                    point_a.y += inc.y;
                    error.x -= delta.x * 2;
                }
                cursors.AddUnchecked(point_a, pinned: true);
            }
        }

        GameObject CreateEntity(GameObject original, string name, Vector3 position) {
#if UNITY_EDITOR
            var entity = (GameObject)PrefabUtility.InstantiatePrefab(original);
#else
            var entity = GameObject.Instantiate(original);
#endif
            Debug.Assert(entity);

            // @Todo: Option to not set sorting order on creation
            entity.GetComponent<SpriteRenderer>().sortingOrder = layer;

            // @Todo: Set format in options
            entity.name = string.Format("{0}_{1:X3}", name, entityid++);
            position.z = chunk.layers[layer].z_depth;
            entity.transform.position = position;

            entity.SetActive(false);
            undo.RegisterState(entity);
            entity.SetActive(true);

            GridAssign(entity, position);
            // @Todo: Invoke(created, created)
            return entity;
        }

        GameObject CreateFromLvlEntity(LvlEntity lvl_entity, Vector3 position) {
            var prefab = palette[lvl_entity.palette_index];
            var entity = CreateEntity(prefab, prefab.name, position);
            var parent = ParentContainer(lvl_entity.palette_index);
            entity.transform.SetParent(parent.transform);

            if (lvl_entity.rotation != 0)
                entity.transform.rotation = Quaternion.Euler(0, 0, lvl_entity.rotation);

            if (lvl_entity.flip_x || lvl_entity.flip_y) {
                var sprite = entity.GetComponent<SpriteRenderer>();
                if (!sprite) {
                    Debug.LogErrorFormat(
                        "[e2d] Missing sprite renderer for {0}.",
                        entity.name);
                    return null;
                }
                sprite.flipX = lvl_entity.flip_x;
                sprite.flipY = lvl_entity.flip_y;
            }
            return entity;
        }

        /// Create an empty game object for a palette entry
        GameObject ParentContainer(int palette_index) {
            Debug.Assert(palette.Length == prefab_parents.Length);
            if (!prefab_parents[palette_index]) {
                var entity = new GameObject(palette[palette_index].name);
                prefab_parents[palette_index] = entity;
                return entity;
            }
            return prefab_parents[palette_index];
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

        void ToggleTransformMode(Mode new_mode) {
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
            mode = new_mode;
        }

        void ToggleBoxMode(Mode new_mode) {
            if (mode == Mode.Box) {
                mode = Mode.Normal;
                cursors.Sync();
                return;
            }

            Vector3 last = cursors[cursors.Count - 1].position;
            selection_rect = new Vector4(last.x, last.y, last.x, last.y);
            mode = new_mode;
        }

        void ToggleLineMode(Mode new_mode) {
            cursors.Sync();
            if (mode == Mode.Line) {
                // Unpin all cursors
                for (int i = 0; i < cursors.Count; i++) {
                    cursors[i] = new Cursor() {
                        position = cursors[i].position,
                        pinned   = false
                    };
                }
                mode = Mode.Normal;
                return;
            }
            LineCreateVertex();
            mode = new_mode;
        }
    }
}
