using System;
using UnityEngine;

namespace Editor2D
{
    public class LevelEditor : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] Camera Camera   = null;

        [Header("GameObject Lookup")]
        [SerializeField] Filter Filter   = Filter.HAS_SPRITE_RENDERER;
        [SerializeField] Sorting Sorting = Sorting.ORDER_IN_LAYER;

        [Header("Grid")]
        [SerializeField] Rect MinArea    = new Rect(-8, -8, 16, 16);
        [Space(4)]
        [SerializeField] Rect MaxArea    = new Rect(-256, -256, 512, 512);
        [Space(8)]
        [SerializeField] float TileSize  = 1;

        [Header("Tile Set")]
        [SerializeField] GameObject[] Palette  = null;

        [Header("Theme")]
        [SerializeField] Font Font             = null;
        [Tooltip("Font size multiplier")]
        [SerializeField] float FontScaling     = 1;
        [SerializeField] Color FontColor       = new Color(0, 0, 0, .8f);

        [SerializeField] GameObject T0Cursor     = null;
        [SerializeField] GameObject T1GridSquare = null;
        [SerializeField] GameObject T2GridActive = null;
        [SerializeField] GameObject T3Background = null;

        [Tooltip("Width of the larger prefab selection screen.")]
        [Range(2, 16)]
        [SerializeField] int PaletteWidth      = 8;
        [Tooltip("Height of the larger prefab selection screen.")]
        [Range(2, 16)]
        [SerializeField] int PaletteHeight     = 6;
        [Range(0, 2)]
        [SerializeField] float PaletteBorder   = .4f;

        [Header("Input")]
        [SerializeField] Keyboard keyboard;

        Buffer buffer;

        void Start() {
            if (!Camera) Camera = Camera.main;

            // @Todo: Don't allocate on Start(), only on first editor open
            var chunk = ChunkUtil.Alloc(TileSize, MinArea, MaxArea, Filter, Sorting);
            buffer = new Buffer(chunk, Palette, Camera);

            var theme = new Overlay.Theme() {
                cursor       = T0Cursor,
                grid_square  = T1GridSquare,
                grid_active  = T2GridActive,
                background   = T3Background,
                border       = PaletteBorder,
                font         = Font,
                font_scaling = FontScaling,
                font_color   = FontColor,
                palette_area = new Vector2Int(PaletteWidth, PaletteHeight)
            };

            Overlay.Initialize(transform, theme, buffer.palette);
            Overlay.DrawCursors(buffer);
            Overlay.DrawPaletteBar(buffer, Camera);
            Overlay.DrawText(buffer);
        }

        void Update() {
            if (buffer == null) return;
            var command = keyboard.HandleKeyPress();
            Eval.Run(command, buffer);

            if (command != Command.NOP) {
                Overlay.DrawCursors(buffer);

                // @Performance: No need to redraw every time
                if (buffer.mode == Buffer.Mode.PALETTE)
                    Overlay.DrawPaletteScreen(buffer, Camera);
                else
                    Overlay.DrawPaletteBar(buffer, Camera);

                Overlay.DrawText(buffer);
            }
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
                buffer.Finalize();
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
