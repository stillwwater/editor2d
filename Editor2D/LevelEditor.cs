using System;
using UnityEngine;

namespace Editor2D
{
    public class LevelEditor : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("The primary camera. If null the editor will attempt to find it on startup.")]
        [SerializeField] Camera Camera   = null;

        [Header("GameObject Lookup")]
        [Tooltip("Determines which game objects are included in the editor grid.")]
        [SerializeField] Filter Filter   = Filter.HAS_SPRITE_RENDERER;
        [Tooltip("Determines which editor layer the game object is placed in.")]
        [SerializeField] Sorting Sorting = Sorting.ORDER_IN_LAYER;

        [Header("Grid")]
        [Tooltip("Smallest editable area in world units.")]
        [SerializeField] Rect MinArea    = new Rect(-8, -8, 16, 16);
        [Space(4)]
        [Tooltip("Largest editable area in world units.")]
        [SerializeField] Rect MaxArea    = new Rect(-256, -256, 512, 512);
        [Space(8)]
        [Tooltip("Tile size (scale) in world units.")]
        [SerializeField] float TileSize  = 1;

        [Header("Flags")]
        [Tooltip("Destroy deleted entities and clear undo history when closing.")]
        [SerializeField] bool DestroyOnClose   = false;
        [Tooltip("Open editor2d on startup.")]
        [SerializeField] bool OpenOnStart      = false;

        [Header("Tile Set")]
        [Tooltip("Prefabs that can be instantiated by the editor.")]
        [SerializeField] GameObject[] Palette  = null;

        [Header("Font")]
        [SerializeField] Font Font             = null;
        [Tooltip("Font size multiplier")]
        [SerializeField] float FontScaling     = 2;
        [SerializeField] Color FontColor       = new Color(0, 0, 0, .8f);

        [Header("Theme")]
        [SerializeField] GameObject T0Cursor     = null;
        [SerializeField] GameObject T1GridSquare = null;
        [SerializeField] GameObject T2GridActive = null;
        [SerializeField] GameObject T3Background = null;

        [Header("Preview")]
        [SerializeField] Overlay.LowerGridDisplay PreviewPosition
                            = Overlay.LowerGridDisplay.Center;

        [Range(1, 16)]
        [SerializeField] int PreviewWidth       = 4;

        [Header("Palette Window")]
        [Tooltip("Width of the sprite selection panel.")]
        [SerializeField] int PaletteWidth      = 8;
        [Tooltip("Height of the sprite selection panel.")]
        [SerializeField] int PaletteHeight     = 6;
        [SerializeField] float PaletteBorder   = .4f;

        [Header("Input")]
        [SerializeField] Keyboard keyboard;

        Buffer buffer;
        bool running;

        void Start() {
            if (OpenOnStart && OpenEditor()) {
                running = true;
                Overlay.DrawCursors(buffer);
                Overlay.DrawPaletteScreen(buffer, Camera);
                Overlay.DrawPaletteBar(buffer, Camera);
                Overlay.DrawText(buffer);
            }
        }

        bool OpenEditor() {
            if (!CanOpen()) {
                Debug.LogError("[e2d] Failed to open editor.");
                return false;
            }

            if (!Camera) Camera = Camera.main;

            if (buffer == null) {
                var chunk = ChunkUtil.Alloc(TileSize, MinArea, MaxArea, Filter, Sorting);
                buffer = new Buffer(chunk, Palette, Camera);

                var theme = new Overlay.Theme() {
                    cursor            = T0Cursor,
                    grid_square       = T1GridSquare,
                    grid_active       = T2GridActive,
                    background        = T3Background,
                    border            = PaletteBorder,
                    font              = Font,
                    font_scaling      = FontScaling,
                    font_color        = FontColor,
                    palette_area      = new Vector2Int(PaletteWidth, PaletteHeight),
                    palette_display   = PreviewPosition,
                    preview_width     = PreviewWidth
                };

                Overlay.Initialize(transform, theme, buffer.palette);
            }
            return true;
        }

        bool CloseEditor() {
            if (buffer == null) return false;
            if (DestroyOnClose) buffer.Free();
            Overlay.ClearScreen();
            return true;
        }

        void Update() {
            var command = keyboard.HandleKeyPress();

            if (command == Command.ToggleOpen) {
                if (running)
                    running = !CloseEditor() && running;
                else
                    running = OpenEditor();
            }

            if (!running || buffer == null || command == Command.Nop)
                return;

            Eval.Run(command, buffer);
            Overlay.DrawCursors(buffer);

            // @Performance: No need to redraw every time
            if (buffer.mode == Buffer.Mode.Palette)
                Overlay.DrawPaletteScreen(buffer, Camera);
            else
                Overlay.DrawPaletteBar(buffer, Camera);

            Overlay.DrawText(buffer);
        }

        bool CanOpen() {
            bool error = false;
            if (!(T0Cursor && T1GridSquare && T2GridActive && T3Background)) {
                Debug.LogError("[e2d] Missing theme assets.");
                error = true;
            }

            if (!Font) {
                Debug.LogError("[e2d] Missing font asset.");
                error = true;
            }
            return !error;
        }

        void Reset() {
            //
            // Fix for age old Unity bug where initialized fields
            // in serializable classes get reset to their default
            // values every time the games starts, even if changes
            // were made in the inspector.
            //
            // Unity forum thread on this issue:
            //     default-values-for-serializable-class-not-supported.42499
            //
            keyboard = new Keyboard();
        }

        void OnApplicationQuit() {
            if (buffer != null) {
                buffer.Free();
            }
        }

        void OnDrawGizmos() {
            DrawBoundsGizmo(MinArea, Color.green);
            DrawBoundsGizmo(MaxArea, Color.red);

            if (buffer != null) {
                DrawBoundsGizmo(buffer.chunk.bounds, Color.blue);
            }
        }

        void DrawBoundsGizmo(Rect area, Color color) {
            Gizmos.color = color;

            Vector3 pos  = new Vector3(
                (area.x + area.width / 2) * TileSize,
                (area.y + area.height / 2) * TileSize);

            Vector3 size = new Vector3(area.width * TileSize, area.height * TileSize);
            Gizmos.DrawWireCube(pos, size);
        }
    }
}
