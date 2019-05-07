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
        FlipHorizontal,
        FlipVertical,
        Undo,
        NormalMode,
        FocusView,
        SelectSimilar,
        SelectItem,
        SelectAll,
        DeselectItem,
        DeselectAll,
        RandomDeselect,
        RandomInc,
        RandomDec,
        CreateVertex,
        ToggleCamera,
        ToggleBoxSelect,
        ToggleLineSelect,
        ToggleWrite,
        ToggleGrab,
        TogglePalette,
        NextModel,
        PreviousModel,
        NextLayer,
        PreviousLayer,
        NewLayer,
        WriteLvl,
        OpenLvl,
        ZoomIn,
        ZoomOut,
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

                case Command.NormalMode:
                    buffer.SwitchMode(Buffer.Mode.Normal);
                    break;

                case Command.ToggleCamera:
                    buffer.SwitchMode(Buffer.Mode.Camera);
                    break;

                case Command.ToggleBoxSelect:
                    buffer.SwitchMode(Buffer.Mode.Box);
                    break;

                case Command.ToggleLineSelect:
                    buffer.SwitchMode(Buffer.Mode.Line);
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

                case Command.Clone:
                    buffer.Clone();
                    break;

                case Command.FlipHorizontal:
                    buffer.Flip(new Vector3(0, 1));
                    break;

                case Command.FlipVertical:
                    buffer.Flip(new Vector3(1, 0));
                    break;

                case Command.Rotate:
                    buffer.Rotate();
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

                case Command.DeselectItem:
                    buffer.RemovePinnedCursor();
                    return;

                case Command.DeselectAll:
                    buffer.DeselectAll();
                    break;

                case Command.RandomDeselect:
                    buffer.RandomDeselect();
                    break;

                case Command.RandomInc:
                    buffer.random_threshold =
                        Mathf.Clamp01(buffer.random_threshold + 0.05f);
                    buffer.log = buffer.random_threshold.ToString("0.00");
                    break;

                case Command.RandomDec:
                    buffer.random_threshold =
                        Mathf.Clamp01(buffer.random_threshold - 0.05f);
                    buffer.log = buffer.random_threshold.ToString("0.00");
                    break;

                case Command.CreateVertex:
                    buffer.LineCreateVertex();
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

                case Command.WriteLvl:
                    buffer.WriteBufferToFile();
                    break;

                case Command.OpenLvl:
                    buffer.LoadBufferFromFile();
                    break;

                case Command.ZoomIn:
                    buffer.ZoomView(+1);
                    break;

                case Command.ZoomOut:
                    buffer.ZoomView(-1);
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
