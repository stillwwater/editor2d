using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor2D
{
    internal struct UndoState
    {
        internal GameObject entity;
        internal Vector3 position;
        internal Vector3 scale;
        internal Quaternion rotation;
        internal Layer layer;
    }

    internal struct UndoFrame
    {
        internal Buffer.Mode mode;
        internal int layer;
        internal List<UndoState> states;
    }

    internal struct Cursor
    {
        internal Vector3 position;
        internal bool pinned;
    }

    public class Buffer
    {
        public enum Mode
        {
            NORMAL,
            VISUAL,
            GRAB,
            ROTATE,
            SCALE,
            BOX,
            CAMERA
        }

        internal readonly GameObject[] palette;
        internal readonly List<UndoFrame> undo;

        internal Chunk chunk;
        internal Mode mode;
        internal int layer;
        internal int palette_index;
        internal List<Cursor> cursors;
        internal Vector3 view;

        int undo_position;
        int entityid;
        GameObject[] selection = new GameObject[16];
        List<GameObject> deletion_pool = new List<GameObject>();

        internal Buffer(Chunk chunk, GameObject[] palette, Vector3 view) {
            this.view = view;
            this.chunk = chunk;
            this.palette = palette;
            cursors = new List<Cursor>(1);
            cursors.Add(new Cursor() { position = Vector3.zero, pinned = false } );
        }

        internal void Finalize() {
            foreach (var entity in deletion_pool) {
                GameObject.Destroy(entity);
            }

            Array.Resize(ref selection, 1);
            DeselectAll();
        }

        internal void SwitchMode(Mode new_mode) {
            switch (new_mode) {
                case Mode.NORMAL: {
                    DeselectAll();
                    break;
                }
                case Mode.GRAB: {
                    if (mode == Mode.GRAB) {
                        mode = Mode.NORMAL;
                        GridRestoreAtCursors(cursors);
                        DeselectAll();
                        return;
                    }
                    break;
                }
            }
            mode = new_mode;
        }

        internal void CreateFromPalette(int index) {
            if (index >= palette.Length || index < 0) {
                Debug.LogErrorFormat("[e2d]: No palette entity at {0}", index);
                return;
            }

            // @Todo: Keep prefab link

            for (int i = 0; i < cursors.Count; i++) {
                var entity = GameObject.Instantiate(palette[index]);
                entity.name = string.Format("{0}_{1}", palette[index].name, entityid.ToString("x3"));
                // @Todo: Invoke(created, created)
                entity.transform.position = cursors[i].position;
                GridAssign(entity, cursors[i].position);
            }
            GridRestoreAtCursors(cursors);
            entityid++;
        }

        internal void Delete() {
            for (int i = 0; i < cursors.Count; i++) {
                Vector2Int grid_pos;

                if (!MapCoordinate(cursors[i].position, out grid_pos)) {
                    continue;
                }

                var entity = Select(grid_pos);
                // @Todo: Free from grid
                Kill(entity);
            }
        }

        internal void Move(GameObject entity, Vector3 offset) {
            GridAssign(entity, entity.transform.position + offset);
            entity.transform.position += offset;
        }

        internal void Transform(Vector3 offset) {
            if (selection.Length < cursors.Count) {
                // Reallocate selection buffer
                Array.Resize(ref selection, cursors.Count);
            }

            for (int i = 0; i < cursors.Count; i++) {
                Vector3 position = cursors[i].position;

                if (!MapCoordinate(position + offset, out _)) {
                    // Moving out of bounds
                    return;
                }

                selection[i] = Select(position);
            }

            for (int i = 0; i < cursors.Count; i++) {
                if (!cursors[i].pinned || mode != Mode.NORMAL) {
                    cursors[i] = new Cursor() {
                        position = cursors[i].position + offset, pinned = false
                    };
                }

                if (!selection[i]) continue; // Empty selection

                switch (mode) {
                    case Mode.GRAB: Move(selection[i], offset); break;
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
                    cursors.Add(new Cursor() { position = new Vector3(x, y), pinned = false } );
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

            cursors.Add(last);
            cursors[cursors.Count - 2] = new Cursor() { position = last.position, pinned = true };
        }

        internal GameObject Select(Vector2Int grid_pos) {
            var temp   = chunk.layers[layer].temp[grid_pos.x, grid_pos.y];
            var entity = chunk.layers[layer].grid[grid_pos.x, grid_pos.y];
            // Temp is checked first.
            return temp ? temp : entity;
        }

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

        void Kill(GameObject entity) {
            if (!entity) return;
            entity.active = false;
            deletion_pool.Add(entity);
        }

        void Revive(GameObject entity) {
            entity.active = true;
            deletion_pool.Remove(entity);
            var other = Select(entity.transform.position);
            if (other) {
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

        /// Store current state
        void PushFrame() {
            var frame = new UndoFrame();
            frame.mode  = mode;
            frame.layer = layer;
            undo.Add(frame);
        }
    }
}
