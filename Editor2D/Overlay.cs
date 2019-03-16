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
            internal GameObject background;
            internal float border;
            internal Font font;
            internal float font_scaling;
            internal Color font_color;
            internal Vector2Int palette_area;
        }

        struct Pool
        {
            internal List<GameObject> cursors;
            internal GameObject[] palette_previews;
            internal SpriteRenderer[] palette_grid;
            internal GameObject palette_grid_cursor;
            internal GameObject background;
        }

        struct TextInfo
        {
            internal Transform canvas;
            internal Text bar_left;
            internal Text bar_right;
            internal Text bar_center;
        }

        const int Palette_GRID_WIDTH = 10;

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
                palette_grid = new SpriteRenderer[Palette_GRID_WIDTH],
                palette_grid_cursor = GameObject.Instantiate(theme.grid_active, parent),
                background = GameObject.Instantiate(theme.background, parent)
            };

            Alloc(pool.cursors, theme.cursor, 1);
            InitializeSprites(pool.palette_grid, theme.grid_square);
            InitializePreviews(palette);
            text = CreateCanvas();
            pool.palette_grid_cursor.name = "palette_cursor_000";
        }

        internal static void DrawPaletteBar(Buffer buffer, Camera camera) {
            Vector3 screen_pixels = new Vector3(Screen.width, Screen.height, 0);
            Vector3 camera_pos    = camera.transform.position;
            Vector3 screen_units  = camera.ScreenToWorldPoint(screen_pixels) - camera_pos;
            int page = buffer.palette_index / pool.palette_grid.Length;

            foreach (var p in pool.palette_previews) {
                p.SetActive(false);
            }

            pool.background.SetActive(false);

            for (int i = 0; i < pool.palette_grid.Length; i++) {
                GameObject entity;

                entity = pool.palette_grid[i].gameObject;
                entity.gameObject.SetActive(true);

                float x = (i - pool.palette_grid.Length / 2f) + .5f + camera_pos.x;
                float y = (1 - screen_units.y) + camera_pos.y;
                entity.transform.position = new Vector3(x, y);

                if (i == buffer.palette_index % pool.palette_grid.Length) {
                    var cursor = pool.palette_grid_cursor;
                    cursor.transform.position = entity.transform.position;
                }

                int index_map = i + pool.palette_grid.Length * page;

                if (index_map >= buffer.palette.Length) {
                    pool.palette_grid[i].color = new Color(.7f, .7f, .7f, 1f);
                    continue;
                }

                var preview = pool.palette_previews[index_map];
                preview.transform.position   = new Vector3(x, y);
                preview.transform.localScale = new Vector3(.8f, .8f, .8f);
                preview.SetActive(true);

                pool.palette_grid[i].color = Color.white;
            }
        }

        internal static void DrawPaletteScreen(Buffer buffer, Camera camera) {
            Vector3 camera_pos = camera.transform.position;
            int width  = theme.palette_area.x;
            int height = theme.palette_area.y;
            Debug.Assert(width > 0 && height > 0);

            foreach (var p in pool.palette_previews) {
                p.SetActive(false);
            }

            foreach (var sp in pool.palette_grid) {
                sp.gameObject.SetActive(false);
            }

            int page = (buffer.palette_index / (width*height));
            int start = page * (width*height);
            int length = Math.Min(buffer.palette.Length, width*height + start);

            pool.background.SetActive(true);
            {
                var bg = pool.background.transform;
                bg.position   = new Vector3(camera_pos.x, camera_pos.y);
                bg.localScale = new Vector3(width+theme.border, height+theme.border, 1f);
            }

            for (int i = start; i < length; i++) {
                float y = (i - start) / width;
                float x = (i - start) - y*width;

                // y inverted to draw palette
                // starting from the top left.
                Vector3 position = new Vector3(
                    x + (camera_pos.x - ((float)width - 1f) / 2f),
                    -y + (camera_pos.y + ((float)height - 1f) / 2f));

                if (i == buffer.palette_index) {
                    pool.palette_grid_cursor.SetActive(true);
                    pool.palette_grid_cursor.transform.position = position;
                }

                var preview = pool.palette_previews[i];
                preview.transform.position   = position;
                preview.transform.localScale = Vector3.one;
                preview.SetActive(true);

            }
        }

        internal static void DrawCursors(Buffer buffer) {
            if (buffer.cursors.Count > pool.cursors.Count) {
                Alloc(pool.cursors, theme.cursor, buffer.cursors.Count - pool.cursors.Count);
            }

            for (int i = 0; i < buffer.cursors.Count; i++) {
                pool.cursors[i].SetActive(true);
                pool.cursors[i].transform.position = buffer.cursors[i].position;
            }

            for (int i = buffer.cursors.Count; i < pool.cursors.Count; i++) {
                if (!pool.cursors[i].activeSelf) {
                    break;
                }
                pool.cursors[i].SetActive(false);
            }
        }

        internal static void DrawText(Buffer buffer) {
            // @Todo: Allow mapped cursor to be displayed.
            Vector3 cursor = buffer.cursors[buffer.cursors.Count - 1].position;
            text.bar_right.text = string.Format("{0},{1}", cursor.x, cursor.y);

            string name = "";
            var selected = buffer.Select(cursor);

            if (selected && buffer.log == null) {
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

            if (buffer.log != null) {
                text.bar_center.text = buffer.log;
                buffer.log = null; // Don't show log next time
                return;
            }

            if (!text.canvas.gameObject.activeSelf)
                text.canvas.gameObject.SetActive(true);

            switch (buffer.mode) {
                case Buffer.Mode.Normal:
                    text.bar_center.text = "";
                    break;

                case Buffer.Mode.Palette:
                    int area = theme.palette_area.x*theme.palette_area.y;
                    int page = (buffer.palette_index / area) + 1;
                    text.bar_center.text = string.Format("~Page {0}~", page);
                    break;

                default:
                    // @Performance: Cache string allocation
                    string mode = buffer.mode.ToString().ToUpper();
                    text.bar_center.text = string.Format("~{0}~", mode);
                    break;
            }
        }

        internal static void ClearScreen() {
            foreach (var p in pool.palette_previews)
                p.SetActive(false);

            foreach (var sp in pool.palette_grid)
                sp.gameObject.SetActive(false);

            foreach (var c in pool.cursors)
                c.SetActive(false);

            pool.background.SetActive(false);
            pool.palette_grid_cursor.SetActive(false);
            text.canvas.gameObject.SetActive(false);
        }

        ///
        /// Simulate movement in a 2d grid structure with a fixed size view.
        /// The buffer's palette is a single dimension array, but it can
        /// be displayed in a grid with multiple 'pages'. To select an item
        /// in the grid the index must be mapped to a position in the buffer's
        /// palette array.
        ///
        internal static int MapToPaletteIndex(Buffer buffer, Vector2 direction) {
            int width = theme.palette_area.x;
            int height = theme.palette_area.y;
            int i = buffer.palette_index;

            // Moving within rows
            // If going over the row limits, select the item adjancent
            // to the current position, on the next or previous page.
            if (direction.x > 0 && i < buffer.palette.Length - 1) {
                if (i % width >= width - 1) {
                    i += width*height - (width - 1); // Next page
                    return Math.Min(i, buffer.palette.Length - 1);
                }
                return i + 1;
            } else if (direction.x < 0 && i > 0) {
                if (i % width <= 0) {
                    i -= width*height - (width - 1); // Previous page
                    // Cannot return a negative index, no change
                    return i >= 0 ? i : buffer.palette_index;
                }
                return i - 1;
            }

            // Moving within columns
            i = Math.Min(i + width*(-(int)direction.y), buffer.palette.Length - 1);
            return i >= 0 ? i : buffer.palette_index;
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
            entity.SetActive(false);
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
            t.color = theme.font_color;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.alignment = align;

            return t;
        }
    }
}
