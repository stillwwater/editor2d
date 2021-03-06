using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor2D
{
    ///
    /// Determines how the level editor puts different
    /// objects in separate layers.
    ///
    /// ORDER_IN_LAYER: Order in layer in sprite renderer
    /// Z_DEPTH: The z axis position.
    ///
    internal enum Sorting
    {
        ORDER_IN_LAYER,
        Z_DEPTH,
    }

    internal struct Layer
    {
        internal GameObject[,] grid;
        internal GameObject[,] temp;
        internal bool visible;
        internal float z_depth;
    }

    internal struct Chunk
    {
        internal float cell_scale;
        internal Rect bounds;
        internal RectInt scaled_bounds;
        internal List<Layer> layers;
    }

    internal static class ChunkUtil
    {
        internal static void Realloc(ref Chunk chunk, int num_layers, float z = 0) {
            int size_x = chunk.scaled_bounds.width;
            int size_y = chunk.scaled_bounds.height;

            for (int i = 0; i < num_layers; i++) {
                chunk.layers.Add(new Layer() {
                    grid = new GameObject[size_x, size_y],
                    temp = new GameObject[size_x, size_y],
                    z_depth = z,
                    visible = true
                });
            }
        }

        /// Finds game objects to add to the buffer
        internal static Chunk Alloc(
            float cell_scale,
            Rect min,
            Rect max,
            Sorting sorting)
        {
            var objects = FindGameObjects(typeof(SpriteRenderer));

            if (objects == null) {
                Debug.LogError("[e2d] No GameObjects found.");
                return new Chunk();
            }

            var bounds = MapEdges(objects, min, max);
            var layer_map = MapLayers(sorting, objects);

            var chunk = new Chunk() {
                bounds = bounds,
                cell_scale = cell_scale,
                scaled_bounds = ScaleBounds(bounds, cell_scale),
                layers = new List<Layer>()
            };

            int size_x = chunk.scaled_bounds.width;
            int size_y = chunk.scaled_bounds.height;

            if (objects.Length == 0) {
                chunk.layers.Add(new Layer() {
                    grid = new GameObject[size_x, size_y],
                    temp = new GameObject[size_x, size_y],
                    visible = true
                });
                return chunk;
            }

            Debug.Assert(layer_map.Length == objects.Length);
            Array.Sort(layer_map, objects);

            for (int i = 0; i < objects.Length; i++) {
                var go = objects[i];
                Vector3 pos = go.transform.position;

                if (pos.x < bounds.x || pos.x > (bounds.x + bounds.width)) continue;
                if (pos.y < bounds.y || pos.y > (bounds.y + bounds.height)) continue;

                if (layer_map[i] >= chunk.layers.Count) {
                    // @Todo: Assumes sequential layers
                    chunk.layers.Add(new Layer() {
                        grid = new GameObject[size_x, size_y],
                        temp = new GameObject[size_x, size_y],
                        z_depth = pos.z,
                        visible = true
                    });
                }

                int x = (int)((pos.x - bounds.x) / cell_scale);
                int y = (int)((pos.y - bounds.y) / cell_scale);
                float z_depth = chunk.layers[layer_map[i]].z_depth;

                if (z_depth != pos.z) {
                    Debug.LogWarningFormat(
                        "[e2d] '{0}' does not have expected z-value of {1}.",
                        go.name,
                        z_depth);
                    go.transform.position = new Vector3(pos.x, pos.y, z_depth);
                }

                chunk.layers[layer_map[i]].grid[x, y] = go;
            }

            return chunk;
        }

        internal static RectInt ScaleBounds(Rect bounds, float scale) {
            return new RectInt() {
                x      = (int)(bounds.x / scale),
                y      = (int)(bounds.y / scale),
                width  = Mathf.CeilToInt(bounds.width / scale),
                height = Mathf.CeilToInt(bounds.height / scale),
            };
        }

        static GameObject[] FindGameObjects(Type component) {
            var objects  = GameObject.FindObjectsOfType<GameObject>();
            var filtered = new List<GameObject>(objects.Length);

            foreach (var go in objects) {
                if (go.GetComponent(component) != null) {
                    filtered.Add(go);
                }
            }

            return filtered.ToArray();
        }

        static int[] MapLayers(Sorting sorting, GameObject[] objects) {
            var layer_map = new int[objects.Length];
            int lower_bound = int.MaxValue;

            for (int i = 0; i < objects.Length; i++) {
                int layer;
                var go = objects[i];

                switch (sorting) {
                    case Sorting.ORDER_IN_LAYER:
                        layer = go.GetComponent<SpriteRenderer>().sortingOrder;
                        break;
                    case Sorting.Z_DEPTH:
                        // Truncating float to int, z-depth 1.1 will be the same as 1.0.
                        layer = (int)go.transform.position.z;
                        break;
                    default:
                        layer = 0;
                        break;
                }

                if (layer < lower_bound) lower_bound = layer;
                layer_map[i] = layer;
            }

            for (int i = 0; i < layer_map.Length; i++) {
                // Layer must be scaled to minimize the number of pre-allocated
                // buffers. Layers 1000, 1001, 1002 become 0, 1, 2.
                layer_map[i] -= lower_bound;
            }

            return layer_map;
        }

        static Rect MapEdges(GameObject[] objects, Rect min, Rect max) {
            float top = Mathf.NegativeInfinity, right = Mathf.NegativeInfinity;
            float bottom = Mathf.Infinity, left = Mathf.Infinity;

            foreach (var obj in objects) {
                Vector3 pos = obj.transform.position;

                if (pos.x < max.x || pos.x > max.x + max.width) continue;
                if (pos.y < max.y || pos.y > max.y + max.height) continue;

                if (pos.y > top) top = pos.y;
                if (pos.y < bottom) bottom = pos.y;

                if (pos.x > right) right = pos.x;
                if (pos.x < left) left = pos.x;
            }

            float clamped_left   = Mathf.Min(left, min.x);
            float clamped_bottom = Mathf.Min(bottom, min.y);
            float clamped_right  = Mathf.Max(right, min.width + min.x);
            float clamped_top    = Mathf.Max(top, min.height + min.y);

            return new Rect() {
                x = clamped_left,
                y = clamped_bottom,
                width = (clamped_right - clamped_left),
                height = (clamped_top - clamped_bottom)
            };
        }
    }
}
