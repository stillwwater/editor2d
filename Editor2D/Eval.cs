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
        NEW_LAYER,
        NOP
    }

    public static class Eval
    {
        internal static void Run(Command cmd, Buffer buffer) {
            switch (cmd) {
                case Command.NOP: break;
                case Command.TRANSFORM_UP: {
                    buffer.Transform(Vector2.up);
                    break;
                }
                case Command.TRANSFORM_DOWN: {
                    buffer.Transform(Vector2.down);
                    break;
                }
                case Command.TRANSFORM_LEFT: {
                    buffer.Transform(Vector2.left);
                    break;
                }
                case Command.TRANSFORM_RIGHT: {
                    buffer.Transform(Vector2.right);
                    break;
                }
                case Command.NEXT_LAYER: {
                    if (buffer.layer >= buffer.chunk.layers.Count - 1) {
                        buffer.layer = 0;
                        break;
                    }
                    buffer.layer++;
                    break;
                }
                case Command.PREVIOUS_LAYER: {
                    if (buffer.layer < 0) {
                        buffer.layer = buffer.chunk.layers.Count - 1;
                        break;
                    }
                    buffer.layer--;
                    break;
                }
                case Command.NEW_LAYER: {
                    ChunkUtil.Realloc(ref buffer.chunk, 1);
                    buffer.layer = buffer.chunk.layers.Count - 1;
                    break;
                }
                case Command.TOGGLE_GRAB: {
                    buffer.SwitchMode(Buffer.Mode.GRAB);
                    break;
                }
                case Command.NORMAL_MODE: {
                    buffer.SwitchMode(Buffer.Mode.NORMAL);
                    break;
                }
                case Command.NEXT_MODEL: {
                    buffer.palette_index++;
                    break;
                }
                case Command.PREVIOUS_MODEL: {
                    buffer.palette_index--;
                    break;
                }
                case Command.WRITE: {
                    buffer.CreateFromPalette(buffer.palette_index);
                    break;
                }
                case Command.DELETE: {
                    buffer.Delete();
                    break;
                }
                case Command.SELECT_ITEM: {
                    if (buffer.mode != Buffer.Mode.NORMAL) {
                        buffer.SwitchMode(Buffer.Mode.NORMAL); 
                    }
                    buffer.PinCursor();
                    break;
                }
                case Command.TOGGLE_ALL: {
                    if (buffer.cursors.Count == 1) {
                        buffer.SelectAllInLayer();
                    } else {
                        buffer.DeselectAll();
                    }
                    break;
                }
                case Command.FOCUS_VIEW: {
                    Vector3 cursor = buffer.cursors[buffer.cursors.Count - 1].position;
                    buffer.view = new Vector3(cursor.x, cursor.y, buffer.view.z);
                    break;
                }
            }
        }
    }
}
