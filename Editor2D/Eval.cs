using UnityEngine;

namespace Editor2D
{
    public enum Command
    {
        TRANSFORM_UP,
        TRANSFORM_DOWN,
        TRANSFORM_LEFT,
        TRANSFORM_RIGHT,
        NEXT_MODEL,
        PREVIOUS_MODEL,
        NEXT_LAYER,
        PREVIOUS_LAYER,
        CLONE,
        CUT,
        COPY,
        PASTE,
        UNDO,
        REDO,
        DELETE,
        WRITE,
        MOVE_NEXT_LAYER,
        MOVE_PREVIOUS_LAYER,
        NORMAL_MODE,
        FOCUS_VIEW,
        TOGGLE_SIMILAR,
        SELECT_ITEM,
        TOGGLE_ALL,
        TOGGLE_CAMERA,
        TOGGLE_BOX_SELECT,
        TOGGLE_WRITE,
        TOGGLE_GRAB,
        TOGGLE_ROTATE,
        TOGGLE_SCALE,
        TOGGLE_HIDE_LAYER,
        TOGGLE_HIDE_OTHER_LAYERS,
        TOGGLE_PALETTE,
        NEW_LAYER,
        NOP
    }

    public static class Eval
    {
        internal static void Run(Command cmd, Buffer buffer) {
            switch (cmd) {
                case Command.NOP: break;
                case Command.TRANSFORM_UP:
                    HandleTransform(buffer, Vector2.up);
                    break;

                case Command.TRANSFORM_DOWN:
                    HandleTransform(buffer, Vector2.down);
                    break;

                case Command.TRANSFORM_LEFT:
                    HandleTransform(buffer, Vector2.left);
                    break;

                case Command.TRANSFORM_RIGHT:
                    HandleTransform(buffer, Vector2.right);
                    break;

                case Command.NEXT_LAYER:
                    if (buffer.layer >= buffer.chunk.layers.Count - 1) {
                        buffer.layer = 0;
                        break;
                    }
                    buffer.layer++;
                    break;

                case Command.PREVIOUS_LAYER:
                    if (buffer.layer <= 0) {
                        buffer.layer = buffer.chunk.layers.Count - 1;
                        break;
                    }
                    buffer.layer--;
                    break;

                case Command.NEW_LAYER:
                    ChunkUtil.Realloc(ref buffer.chunk, 1);
                    buffer.layer = buffer.chunk.layers.Count - 1;
                    break;

                case Command.TOGGLE_GRAB:
                    buffer.SwitchMode(Buffer.Mode.GRAB);
                    break;

                case Command.NORMAL_MODE:
                    buffer.SwitchMode(Buffer.Mode.NORMAL);
                    break;

                case Command.NEXT_MODEL:
                    if (buffer.palette_index < buffer.palette.Length - 1) {
                        buffer.palette_index++;
                    }
                    break;

                case Command.PREVIOUS_MODEL:
                    if (buffer.palette_index > 0)
                        buffer.palette_index--;
                    break;

                case Command.WRITE:
                    buffer.CreateFromPalette(buffer.palette_index);
                    break;

                case Command.DELETE:
                    buffer.Delete();
                    break;

                case Command.SELECT_ITEM:
                    if (buffer.mode != Buffer.Mode.NORMAL) {
                        buffer.SwitchMode(Buffer.Mode.NORMAL);
                    }
                    buffer.PinCursor();
                    break;

                case Command.TOGGLE_ALL:
                    if (buffer.cursors.Count == 1)
                        buffer.SelectAllInLayer();
                    else
                        buffer.DeselectAll();
                    break;

                case Command.UNDO:
                    if (buffer.undo.PopFrame(out Undo.Frame frame)) {
                        buffer.Revert(frame);
                    }
                    break;

                case Command.FOCUS_VIEW:
                    buffer.FocusAtCursors();
                    break;

                case Command.TOGGLE_PALETTE:
                    buffer.SwitchMode(Buffer.Mode.PALETTE);
                    break;
            }
        }

        static void HandleTransform(Buffer buffer, Vector2 direction) {
            if (buffer.mode == Buffer.Mode.PALETTE) {
                int i = Overlay.MapToPaletteIndex(buffer, direction);
                buffer.palette_index = i;
                return;
            }
            buffer.Transform(direction * buffer.chunk.cell_scale);
        }
    }
}
