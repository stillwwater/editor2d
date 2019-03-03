using System;
using UnityEngine;

namespace Editor2D
{
    public class LevelEditor : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] Camera Camera;

        [Header("GameObject Lookup")]
        [SerializeField] Filter Filter;
        [SerializeField] Sorting Sorting;

        [Header("Grid")]
        [SerializeField] Rect MinArea = new Rect(-8, -8, 16, 16);
        [Space(4)]
        [SerializeField] Rect MaxArea = new Rect(-256, -256, 512, 512);
        [Space(8)]
        [SerializeField] float TileSize = 1;

        [Header("Tile Set")]
        [SerializeField] GameObject[] Palette;

        [Header("Theme")]
        [SerializeField] GameObject Cursor;
        [SerializeField] GameObject GridSquare;
        [SerializeField] GameObject GridActive;

        [Header("Input")]
        [SerializeField] Keyboard keyboard;

        GameObject[] cursor_pool = new GameObject[32]; // @Temporary
        Buffer buffer;

        void Start() {
            // @Todo: Don't allocate on Start(), only on first editor open
            var chunk = ChunkUtil.Alloc(TileSize, MinArea, MaxArea, Filter, Sorting);
            buffer = new Buffer(chunk, Palette, Camera.transform.position);

            var theme = new Overlay.Theme() {
                cursor = Cursor,
                grid_square = GridSquare,
                grid_active = GridActive
            };

            Overlay.Initialize(transform, theme, buffer.palette);
            Overlay.DrawCursors(buffer);
            Overlay.DrawPaletteGrid(buffer, Camera);
        }

        void Update() {
            if (buffer == null) return;
            var command = keyboard.HandleKeyPress();
            Eval.Run(command, buffer);

            if (command != Command.NOP) {
                Camera.transform.position = buffer.view;
                Overlay.DrawCursors(buffer);
                // @Performance: No need to redraw every time
                Overlay.DrawPaletteGrid(buffer, Camera);
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
                (area.y + area.height / 2) * TileSize
            );
            Vector3 size = new Vector3(area.width * TileSize, area.height * TileSize);
            Gizmos.DrawWireCube(pos, size);
        }
    }
}
