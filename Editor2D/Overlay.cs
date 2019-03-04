using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Editor2D
{
    internal static class Overlay
    {
        internal struct Theme
        {
            internal GameObject cursor;
            internal GameObject grid_square;
            internal GameObject grid_active;
            internal Font font;
            internal float font_scaling;
            internal Color text_color;
        }

        struct Pool
        {
            internal List<GameObject> cursors;
            internal GameObject[] palette_previews;
            internal SpriteRenderer[] palette_grid;
            internal GameObject palette_grid_cursor;
        }

        struct TextInfo
        {
            internal Transform canvas;
            internal Text bar_left;
            internal Text bar_right;
            internal Text bar_center;
        }

        const int PALETTE_GRID_WIDTH = 10;

        static Transform parent;
        static TextInfo text;
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
            text = CreateCanvas();
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

        internal static void DrawText(Buffer buffer) {
            // @Todo: Allow mapped cursor to be displayed.
            Vector3 cursor = buffer.cursors[buffer.cursors.Count - 1].position;
            text.bar_right.text = string.Format("{0},{1}", cursor.x, cursor.y);

            string name = "";
            var selected = buffer.Select(cursor);

            if (selected) {
                if (buffer.cursors.Count > 1)
                    name = selected.name + " etc.";
                else
                    name = selected.name;
            }

            if (name.Length > 15) {
                string start = name.Substring(0, 6);
                string end = name.Substring(name.Length - 6);
                name = string.Format("{0}...{1}", start, end);
            }

            text.bar_left.text = string.Format("L: {0} {1}", buffer.layer, name);

            if (buffer.mode == Buffer.Mode.NORMAL)
                text.bar_center.text = "";
            else
                text.bar_center.text = string.Format("~{0}~", buffer.mode);
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

        static TextInfo CreateCanvas() {
            var info = new TextInfo();
            var entity = new GameObject("canvas_000");

            var canvas = entity.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var raycaster = entity.AddComponent<GraphicRaycaster>();
            var scalar = entity.AddComponent<CanvasScaler>();
            scalar.scaleFactor = 1f;
            scalar.dynamicPixelsPerUnit = 100f;

            var rt = entity.GetComponent<RectTransform>();
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0f);
            rt.SetParent(parent);

            info.canvas = entity.transform;

            info.bar_right = CreateText(
                name:  "bar_right",
                size:   new Vector2(200, 32),
                fsize:  32,
                offset: new Vector3(32, 32),
                align:  TextAnchor.UpperRight,
                canvas: canvas);

            info.bar_left = CreateText(
                name:   "bar_left",
                size:   new Vector2(200, 32),
                fsize:  32,
                offset: new Vector3(32, 32),
                align:  TextAnchor.UpperLeft,
                canvas: canvas);

            info.bar_center = CreateText(
                name:   "bar_center",
                size:   new Vector2(200, 32),
                fsize:  32,
                offset: new Vector3(0, 32),
                align:  TextAnchor.UpperCenter,
                canvas: canvas);

            return info;
        }

        static Text CreateText(
            string name,
            Vector2 size,
            int fsize,
            Vector3 offset,
            TextAnchor align,
            Canvas canvas)
        {
            var text_entity = new GameObject(name);
            text_entity.transform.parent = canvas.transform;

            var t = text_entity.AddComponent<Text>();
            var rt = t.GetComponent<RectTransform>();

            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);
            rt.sizeDelta = size;

            float ax = 0, ay = 0;

            switch (align) {
                case TextAnchor.UpperLeft:
                    rt.anchorMax = new Vector2(0, 1);
                    ax = size.x / 2 + offset.x;
                    ay = -size.y / 2 - offset.y;
                    break;

                case TextAnchor.UpperCenter:
                    rt.anchorMax = new Vector2(.5f, 1);
                    ax = offset.x;
                    ay = -size.y / 2 - offset.y;
                    break;

                case TextAnchor.UpperRight:
                    rt.anchorMax = new Vector2(1, 1);
                    ax = -size.x / 2 - offset.x;
                    ay = -size.y / 2 - offset.y;
                    break;
            }

            rt.anchorMin = rt.anchorMax;
            rt.anchoredPosition = new Vector3(ax, ay);

            t.font = theme.font;
            t.fontSize = (int)(fsize * theme.font_scaling);
            t.text = "---";
            t.color = theme.text_color;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.alignment = align;

            return t;
        }
    }
}
