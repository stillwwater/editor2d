using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Editor2D
{
    internal static class Overlay
    {
        internal struct Theme
        {
            internal GameObject cursor;
            internal GameObject grid_square;
            internal GameObject grid_active;
        }

        struct Pool
        {
            internal List<GameObject> cursors;
            internal GameObject[] palette_previews;
            internal SpriteRenderer[] palette_grid;
            internal GameObject palette_grid_cursor;
        }

        const int PALETTE_GRID_WIDTH = 10;

        static Transform parent;
        static Theme theme;
        static Pool pool;

        internal static void Initialize(Transform parent, Theme theme, GameObject[] palette) {
            Overlay.parent = parent;
            Overlay.theme  = theme;

            pool = new Pool() {
                cursors = new List<GameObject>(),
                palette_previews = new GameObject[palette.Length],
                palette_grid = new SpriteRenderer[PALETTE_GRID_WIDTH],
                palette_grid_cursor = GameObject.Instantiate(theme.grid_active, parent),
            };

            Alloc(pool.cursors, theme.cursor, 1);
            InitializeSprites(pool.palette_grid, theme.grid_square);
            InitializePreviews(palette);
            pool.palette_grid_cursor.name = "palette_cursor_000";
        }

        internal static void DrawPaletteGrid(Buffer buffer, Camera camera) {
            Vector3 screen_pixels = new Vector3(Screen.width, Screen.height, 0);
            Vector3 camera_pos    = camera.transform.position;
            Vector3 screen_units  = camera.ScreenToWorldPoint(screen_pixels) - camera_pos;
            int page = buffer.palette_index / pool.palette_grid.Length;

            foreach (var p in pool.palette_previews) {
                p.active = false;
            }

            for (int i = 0; i < pool.palette_grid.Length; i++) {
                GameObject entity;
                SpriteRenderer sprite;

                if (i == buffer.palette_index % pool.palette_grid.Length) {
                    entity = pool.palette_grid_cursor;
                    pool.palette_grid[i].gameObject.active = false;
                } else {
                    entity = pool.palette_grid[i].gameObject;
                    entity.gameObject.active = true;
                }

                float x = (i - pool.palette_grid.Length / 2f) + .5f + camera_pos.x;
                float y = (1 - screen_units.y) + camera_pos.y;
                entity.transform.position = new Vector3(x, y);

                int index_map = i + pool.palette_grid.Length * page;

                if (index_map >= buffer.palette.Length) {
                    // @Temporary: Use shader to decrease saturation instead.
                    pool.palette_grid[i].color = new Color(.7f, .7f, .7f, 1f);
                    continue;
                }

                var preview = pool.palette_previews[index_map];
                preview.transform.position = new Vector3(x, y);
                preview.active = true;
                preview.transform.localScale = new Vector3(.8f, .8f, .8f);

                pool.palette_grid[i].color = Color.white;
            }
        }

        internal static void DrawCursors(Buffer buffer) {
            if (buffer.cursors.Count > pool.cursors.Count) {
                Alloc(pool.cursors, theme.cursor, buffer.cursors.Count - pool.cursors.Count);
            }

            for (int i = 0; i < buffer.cursors.Count; i++) {
                pool.cursors[i].active = true;
                pool.cursors[i].transform.position = buffer.cursors[i].position;
            }

            for (int i = buffer.cursors.Count; i < pool.cursors.Count; i++) {
                if (!pool.cursors[i].active) {
                    break;
                }
                pool.cursors[i].active = false;
            }
        }

        static void InitializeSprites(SpriteRenderer[] items, GameObject original) {
            for (int i = 0; i < items.Length; i++) {
                var entity = Alloc(original, i);
                var sprite = entity.GetComponent<SpriteRenderer>();

                if (!sprite) {
                    Debug.LogErrorFormat("[e2d] {0} is missing a SpriteRenderer.", entity.name);
                    continue;
                }

                items[i] = sprite;
            }
        }

        static void InitializePreviews(GameObject[] palette) {
            Debug.Assert(palette.Length == pool.palette_previews.Length);

            for (int i = 0; i < palette.Length; i++) {
                var entity = Alloc(null, i, "preview");

                // Copy only palette object visuals so that other behaviour
                // is not also inherited by the preview.
                var sprite_renderer = CopyComponent<SpriteRenderer>(entity, palette[i]);

                if (sprite_renderer) {
                    entity.GetComponent<SpriteRenderer>().sortingOrder = 1002;
                    pool.palette_previews[i] = entity;
                    continue;
                }

                CopyComponent<MeshFilter>(entity, palette[i]);

                if (!CopyComponent<MeshRenderer>(entity, palette[i]))
                    Debug.LogWarningFormat("[e2d] Palette {0} has no renderer", entity.name);

                pool.palette_previews[i] = entity;
            }
        }

        static void Alloc(List<GameObject> items, GameObject original, int size) {
            for (int i = 0; i < size; i++) {
                int id = (items.Count - 1) + i;
                var entity = Alloc(original, id);
                items.Add(entity);
            }
        }

        static GameObject Alloc(GameObject original, int id, string prefix = "") {
            GameObject entity;

            if (original) {
                entity = GameObject.Instantiate(original, parent);
            } else {
                entity = new GameObject("");
                entity.transform.parent = parent;
                original = entity;
            }

            entity.name = string.Format("{0}{1}_{2}", prefix, original.name, id.ToString("x3"));
            entity.active = false;
            return entity;
        }

        static T CopyComponent<T>(GameObject dst, GameObject src) where T : Component {
            var src_comp = src.GetComponent<T>();

            if (!src_comp)
                return null;

            var dst_comp = dst.AddComponent<T>();
            var type = dst_comp.GetType();

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                        BindingFlags.Default | BindingFlags.DeclaredOnly;

            var properties = type.GetProperties(flags);
            var fields = type.GetFields(flags);

            foreach (var p in properties) {
                if (p.CanWrite)
                    p.SetValue(dst_comp, p.GetValue(src_comp, null), null);
            }

            foreach (var f in fields) {
                f.SetValue(dst_comp, f.GetValue(src_comp));
            }

            return src_comp as T;
        }
    }
}
