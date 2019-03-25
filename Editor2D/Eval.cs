using UnityEngine;

namespace Editor2D
{
    public enum Command
    {
        ToggleOpen,
        Up,
        Down,
        Left,
        Right,
        Write,
        Erase,
        Clone,
        Rotate,
        Flip,
        Undo,
        NormalMode,
        FocusView,
        SelectSimilar,
        SelectItem,
        SelectAll,
        DeselectAll,
        ToggleCamera,
        ToggleBoxSelect,
        ToggleWrite,
        ToggleGrab,
        ToggleScale,
        TogglePalette,
        NextModel,
        PreviousModel,
        NextLayer,
        PreviousLayer,
        NewLayer,
        Nop
    }

    public static class Eval
    {
        internal static void Run(Command cmd, Buffer buffer) {
            switch (cmd) {
                case Command.Nop: break;
                case Command.Up:
                    HandleTransform(buffer, Vector2.up);
                    break;

                case Command.Down:
                    HandleTransform(buffer, Vector2.down);
                    break;

                case Command.Left:
                    HandleTransform(buffer, Vector2.left);
                    break;

                case Command.Right:
                    HandleTransform(buffer, Vector2.right);
                    break;

                case Command.NextLayer:
                    if (buffer.layer >= buffer.chunk.layers.Count - 1) {
                        buffer.layer = 0;
                        break;
                    }
                    buffer.layer++;
                    break;

                case Command.PreviousLayer:
                    if (buffer.layer <= 0) {
                        buffer.layer = buffer.chunk.layers.Count - 1;
                        break;
                    }
                    buffer.layer--;
                    break;

                case Command.NewLayer:
                    ChunkUtil.Realloc(ref buffer.chunk, 1);
                    buffer.layer = buffer.chunk.layers.Count - 1;
                    break;

                case Command.ToggleGrab:
                    buffer.SwitchMode(Buffer.Mode.Grab);
                    break;

                case Command.ToggleScale:
                    buffer.SwitchMode(Buffer.Mode.Scale);
                    break;

                case Command.NormalMode:
                    buffer.SwitchMode(Buffer.Mode.Normal);
                    break;

                case Command.ToggleBoxSelect:
                    buffer.SwitchMode(Buffer.Mode.Box);
                    break;

                case Command.NextModel:
                    if (buffer.palette_index < buffer.palette.Length - 1) {
                        buffer.palette_index++;
                    }
                    break;

                case Command.PreviousModel:
                    if (buffer.palette_index > 0)
                        buffer.palette_index--;
                    break;

                case Command.Write:
                    buffer.CreateFromPalette(buffer.palette_index);
                    break;

                case Command.Erase:
                    buffer.Erase();
                    break;

                case Command.SelectItem:
                    if (buffer.mode != Buffer.Mode.Normal) {
                        buffer.SwitchMode(Buffer.Mode.Normal);
                    }
                    buffer.PinCursor();
                    break;

                case Command.SelectSimilar:
                    buffer.SelectSimilar();
                    break;

                case Command.SelectAll:
                    buffer.SelectAllInLayer();
                    break;

                case Command.DeselectAll:
                    buffer.DeselectAll();
                    break;

                case Command.Undo:
                    if (buffer.undo.PopFrame(out Undo.Frame frame)) {
                        buffer.Revert(frame);
                        break;
                    }
                    buffer.log = "Already at oldest change";
                    break;

                case Command.FocusView:
                    buffer.FocusAtCursors();
                    break;

                case Command.TogglePalette:
                    buffer.SwitchMode(Buffer.Mode.Palette);
                    break;
            }
        }

        static void HandleTransform(Buffer buffer, Vector2 direction) {
            if (buffer.mode == Buffer.Mode.Palette) {
                int i = Overlay.MapToPaletteIndex(buffer, direction);
                buffer.palette_index = i;
                return;
            }
            buffer.Transform(direction * buffer.chunk.cell_scale);
        }
    }
}
