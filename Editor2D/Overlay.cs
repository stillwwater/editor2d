using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Editor2D
{
    internal static class Overlay
    {
        internal enum PreviewDisplay
        {
            Left,
            Center,
            Right,
            Hidden
        }

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
            internal Color status_color;
            internal int status_padding;
            internal Vector2Int palette_area;
            internal PreviewDisplay palette_display;
            internal bool show_preview_text;
            internal int preview_width;
        }

        struct Pool
        {
            internal List<GameObject> cursors;
            internal SpriteRenderer[] palette_previews;
            internal SpriteRenderer[] palette_grid;
            internal GameObject palette_grid_cursor;
            internal GameObject background;
        }

        struct CanvasUI
        {
            internal Transform canvas;
            internal Text bar_left;
            internal Text bar_right;
            internal Text bar_center;
            internal Text preview_index;
            internal Image panel;
            internal Image mini_preview;
            internal Image mini_preview_bg;
        }

        static Transform parent;
        static CanvasUI ui;
        static Theme theme;
        static Pool pool;

        internal static void Initialize(Transform parent, Theme theme, GameObject[] palette) {
            Overlay.parent = parent;
            Overlay.theme  = theme;

            pool = new Pool() {
                cursors = new List<GameObject>(),
                palette_previews = new SpriteRenderer[palette.Length],
                palette_grid = new SpriteRenderer[theme.preview_width],
                palette_grid_cursor = GameObject.Instantiate(theme.grid_active, parent),
                background = GameObject.Instantiate(theme.background, parent)
            };

            Alloc(pool.cursors, theme.cursor, 1);
            InitializeSprites(pool.palette_grid, theme.grid_square);
            InitializePreviews(palette);
            ui = CreateCanvas();
            pool.palette_grid_cursor.name = "palette_cursor_000";
        }

        internal static void DrawPaletteBar(Buffer buffer, Camera camera) {
            foreach (var p in pool.palette_previews) {
                p.gameObject.SetActive(false);
            }

            pool.background.SetActive(false);

            switch (theme.palette_display) {
                case PreviewDisplay.Hidden:
                    // Don't draw previews
                    pool.palette_grid_cursor.SetActive(false);
                    return;

                case PreviewDisplay.Left:
                case PreviewDisplay.Right:
                    // Using canvas panel for preview, saves some computation
                    var sprite = pool.palette_previews[buffer.palette_index].sprite;
                    ui.mini_preview.sprite = sprite;
                    pool.palette_grid_cursor.SetActive(false);
                    return;
            }

            Vector3 screen_pixels = new Vector3(Screen.width, Screen.height, 0);
            Vector3 camera_pos    = camera.transform.position;
            Vector3 screen_units  = camera.ScreenToWorldPoint(screen_pixels) - camera_pos;
            int page = buffer.palette_index / pool.palette_grid.Length;
            pool.palette_grid_cursor.SetActive(true);

            for (int i = 0; i < pool.palette_grid.Length; i++) {
                GameObject entity;
                float x = (i - pool.palette_grid.Length / 2f) + .5f + camera_pos.x;
                float y = (1 - screen_units.y) + camera_pos.y;

                entity = pool.palette_grid[i].gameObject;
                entity.SetActive(true);
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

                var preview = pool.palette_previews[index_map].gameObject;
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
                p.gameObject.SetActive(false);
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

                // @Performance: preview.gameObject seems to be marginally slower than having
                //   cached GameObjects using a rough benchmark. Need to benchmark further to
                //   determine whether it's worth using more memory and cache all GameObjects
                //   for previews.
                var preview = pool.palette_previews[i].gameObject;
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
            float z_depth = buffer.chunk.layers[buffer.layer].z_depth;
            string z = z_depth == 0 ? "" : string.Format(",{0}", z_depth);
            ui.bar_right.text = string.Format("{0},{1}{2}", cursor.x, cursor.y, z);

            string name = "";
            var selected = buffer.Select(cursor);

            if (selected && buffer.log == null) {
                if (buffer.cursors.Count > 1)
                    name = TrimText(selected.name + " etc.", 15);
                else
                    name = TrimText(selected.name, 15);
            }

            if (ui.preview_index)
                ui.preview_index.text = string.Format("#{0:X2}", buffer.palette_index);

            ui.bar_left.text = string.Format("L: {0} {1}", buffer.layer, name);

            if (buffer.log != null) {
                ui.bar_center.text = buffer.log;
                buffer.log = null; // Don't show log next time
                return;
            }

            if (!ui.canvas.gameObject.activeSelf)
                ui.canvas.gameObject.SetActive(true);

            switch (buffer.mode) {
                case Buffer.Mode.Normal:
                    ui.bar_center.text = "";
                    break;

                case Buffer.Mode.Palette:
                    int area = theme.palette_area.x*theme.palette_area.y;
                    int page = (buffer.palette_index / area) + 1;
                    ui.bar_center.text = string.Format("~Page {0}~", page);
                    break;

                default:
                    // @Performance: Cache string allocation
                    string mode = buffer.mode.ToString().ToUpper();
                    ui.bar_center.text = string.Format("~{0}~", mode);
                    break;
            }
        }

        internal static void ClearScreen() {
            foreach (var p in pool.palette_previews)
                p.gameObject.SetActive(false);

            foreach (var sp in pool.palette_grid)
                sp.gameObject.SetActive(false);

            foreach (var c in pool.cursors)
                c.SetActive(false);

            pool.background.SetActive(false);
            pool.palette_grid_cursor.SetActive(false);
            ui.canvas.gameObject.SetActive(false);
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

        static string TrimText(string str, int length) {
            const string sep = "...";

            if (str.Length > length) {
                int pad = (length - sep.Length) / 2;
                string start = str.Substring(0, pad);
                string end = str.Substring(str.Length - pad);
                return string.Format("{0}{1}{2}", start, sep, end);
            }
            return str;
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
                    pool.palette_previews[i] = entity.GetComponent<SpriteRenderer>();
                    continue;
                }

                string name = palette[i].name;
                Debug.LogErrorFormat("[e2d] {0} is missing a SpriteRenderer.", name);
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

            entity.name = string.Format("{0}{1}_{2:x3}", prefix, original.name, id);
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
                // Cannot copy mesh property
                if (p.CanWrite)
                    p.SetValue(dst_comp, p.GetValue(src_comp, null), null);
            }

            foreach (var f in fields) {
                f.SetValue(dst_comp, f.GetValue(src_comp));
            }

            return src_comp as T;
        }

        static CanvasUI CreateCanvas() {
            var info = new CanvasUI();
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

            if (theme.palette_display == PreviewDisplay.Left
                || theme.palette_display == PreviewDisplay.Right) {
                CreateMiniPreview(ref info, canvas);
            }

            CreateStatusBar(ref info, canvas);
            return info;
        }

        static void CreateStatusBar(ref CanvasUI info, Canvas canvas) {
            float hpad = Mathf.Max(32, theme.status_padding + 16);

            info.panel = CreatePanel("status_bar", theme.status_color, canvas);
            CreateUI(
                entity: info.panel,
                size:   new Vector2(
                            Screen.width - theme.status_padding*2,
                            32*theme.font_scaling),
                offset: new Vector3(0, theme.status_padding),
                align:  TextAnchor.UpperCenter);

            info.bar_right = CreateText(
                name:  "bar_right",
                fsize:  48,
                align:  TextAnchor.MiddleRight,
                c:      canvas);

            CreateUI(
                entity: info.bar_right,
                size:   new Vector2(200, 32*theme.font_scaling),
                offset: new Vector3(hpad, theme.status_padding),
                align:  TextAnchor.UpperRight);

            info.bar_left = CreateText(
                name:  "bar_left",
                fsize:  48,
                align:  TextAnchor.MiddleLeft,
                c:      canvas);

            CreateUI(
                entity: info.bar_left,
                size:   new Vector2(200, 32*theme.font_scaling),
                offset: new Vector3(hpad, theme.status_padding),
                align:  TextAnchor.UpperLeft);

            info.bar_center = CreateText(
                name:  "bar_center",
                fsize:  48,
                align:  TextAnchor.MiddleCenter,
                c:      canvas);

            CreateUI(
                entity: info.bar_center,
                size:   new Vector2(200, 32*theme.font_scaling),
                offset: new Vector3(0, theme.status_padding),
                align:  TextAnchor.UpperCenter);
        }

        static void CreateMiniPreview(ref CanvasUI info, Canvas canvas) {
            info.mini_preview_bg = CreatePanel("palette_mini_bg", theme.status_color, canvas);
            info.mini_preview = CreatePanel("palette_mini_fg", Color.white, canvas);

            var align = theme.palette_display == PreviewDisplay.Left
                ? TextAnchor.LowerLeft
                : TextAnchor.LowerRight;

            float panel_width = 70f;

            if (theme.show_preview_text) {
                info.preview_index = CreateText(
                    name:  "preview_index",
                    fsize:  48,
                    align:  align,
                    c:      canvas);

                CreateUI(
                    entity: info.preview_index,
                    size:   new Vector2(192, 54) * theme.font_scaling,
                    offset: new Vector3(100, 40) * theme.font_scaling,
                    align:  align);

                panel_width = 130f;
            }

            CreateUI(
                entity: info.mini_preview_bg,
                size:   new Vector2(panel_width, 70) * theme.font_scaling,
                offset: new Vector3(32, 32) * theme.font_scaling,
                align:  align);

            CreateUI(
                entity: info.mini_preview,
                size:   new Vector2(54, 54) * theme.font_scaling,
                offset: new Vector3(40, 40) * theme.font_scaling,
                align:  align);
        }

        static RectTransform CreateUI<T>(
            T entity,
            Vector2 size,
            Vector3 offset,
            TextAnchor align) where T : Component
        {
            var rt = entity.GetComponent<RectTransform>();
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

                case TextAnchor.LowerLeft:
                    rt.anchorMax = new Vector2(0, 0);
                    ax = size.x / 2 + offset.x;
                    ay = size.y / 2 + offset.y;
                    break;

                case TextAnchor.LowerRight:
                    rt.anchorMax = new Vector2(1, 0);
                    ax = -size.x / 2 - offset.x;
                    ay = size.y / 2 + offset.y;
                    break;

                case TextAnchor.MiddleCenter: break;
                default:
                    Debug.LogErrorFormat("[e2d] Unsopported anchor {0}.", align);
                    break;
            }

            rt.anchorMin = rt.anchorMax;
            rt.anchoredPosition = new Vector3(ax, ay);
            return rt;
        }

        static Text CreateText(string name, int fsize, TextAnchor align, Canvas c) {
            var entity = new GameObject(name);
            var t = entity.AddComponent<Text>();
            entity.transform.SetParent(c.transform);

            t.font = theme.font;
            t.fontSize = (int)(fsize * theme.font_scaling);
            t.text = "---";
            t.color = theme.font_color;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.alignment = align;
            t.alignByGeometry = true;

            return t;
        }

        static Image CreatePanel(string name, Color color, Canvas c) {
            var entity = new GameObject(name);
            var img = entity.AddComponent<Image>();
            entity.transform.SetParent(c.transform);

            img.sprite = null;
            img.raycastTarget = false;
            img.color = color;
            return img;
        }
    }
}
