using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor2D
{
    ///
    /// Determines how the editor finds game objects to
    /// include into its buffer.
    ///
    internal enum Filter
    {
        HAS_SPRITE_RENDERER,
        HAS_MESH_RENDERER,
        HAS_EDITOR_TAG
    }

    ///
    /// Determined how the level editor puts different
    /// objects in layers. If not using an editor tag
    /// component the layer is determined by either:
    ///
    /// SORTING_LAYER: Sorting layer in sprite renderer
    /// ORDER_IN_LAYER: Ordere in layer in sprite renderer
    /// Z_DEPTH: The z axis position.
    ///
    internal enum Sorting
    {
        SORTING_LAYER,
        ORDER_IN_LAYER,
        Z_DEPTH,
    }
    
    internal struct Layer
    {
        internal GameObject[,] grid;
        internal GameObject[,] temp;
        internal bool visible;
    }

    internal struct Chunk
    {
        internal float cell_scale;
        internal Rect bounds;
        internal List<Layer> layers;

        internal RectInt ScaledBounds {
            get => new RectInt() {
                x      = (int)(bounds.x / cell_scale),
                y      = (int)(bounds.y / cell_scale),
                width  = Mathf.CeilToInt(bounds.width / cell_scale),
                height = Mathf.CeilToInt(bounds.height / cell_scale),
            };
        }
    }

    internal static class ChunkUtil
    {
        internal static void Realloc(ref Chunk chunk, int num_layers) {
            int size_x = chunk.ScaledBounds.width + 1;
            int size_y = chunk.ScaledBounds.height + 1;

            for (int i = 0; i < num_layers; i++) {
                chunk.layers.Add(new Layer() {
                    grid = new GameObject[size_x, size_y],
                    temp = new GameObject[size_x, size_y],
                    visible = true
                });
            }
        }

        /// Finds game objects to add to the buffer
        internal static Chunk Alloc(
            float cell_scale,
            Rect min,
            Rect max,
            Filter filter,
            Sorting sorting)
        {
            var objects = FindGameObjects(filter);

            if (objects == null) {
                Debug.LogError("[e2d] No GameObjects found");
                return new Chunk();
            }

            var bounds = MapEdges(objects, min, max);
            var layer_map = MapLayers(sorting, objects);

            var chunk = new Chunk() { 
                bounds = bounds,
                cell_scale = cell_scale,
                layers = new List<Layer>()
            };

            int size_x = chunk.ScaledBounds.width + 1;
            int size_y = chunk.ScaledBounds.height + 1;

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
                        visible = true
                    });
                }

                int x = (int)((pos.x - bounds.x) / cell_scale);
                int y = (int)((pos.y - bounds.y) / cell_scale);

                chunk.layers[layer_map[i]].grid[x, y] = go;
            }

            return chunk;
        }

        static GameObject[] FindGameObjects(Filter filter) {
            var objects  = GameObject.FindObjectsOfType<GameObject>();
            var filtered = new List<GameObject>(objects.Length);
            Type component;

            switch (filter) {
                case Filter.HAS_EDITOR_TAG:
                    component = typeof(LevelEditor);
                    break;
                case Filter.HAS_MESH_RENDERER:
                    component = typeof(MeshRenderer);
                    break;   
                case Filter.HAS_SPRITE_RENDERER:
                default:
                    component = typeof(SpriteRenderer);
                    break;
            }

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
                    case Sorting.SORTING_LAYER:
                        layer = go.GetComponent<SpriteRenderer>().sortingLayerID;
                        break;
                    default:
                    case Sorting.Z_DEPTH:
                        // Truncating float to int, z-depth 1.1 will be the same as 1.0.
                        layer = (int)go.transform.position.z;
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
