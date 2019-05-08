using UnityEngine;
using System.Collections.Generic;
using System;

namespace Editor2D
{
    [Serializable]
    public class Keyboard
    {
        [Serializable]
        public enum Modifier
        {
            None,
            Control,
            Shift,
            Alt
        }

        [Serializable]
        public struct KeyBinding
        {
            public Modifier modifier;
            public KeyCode keycode;
            public Command command;

            public KeyBinding(Modifier mod, KeyCode key, Command cmd) {
                modifier = mod;
                keycode  = key;
                command  = cmd;
            }
        }

        static KeyCode[] valid_keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        static KeyBinding null_key  = new KeyBinding(Modifier.None, KeyCode.None, Command.Nop);

        [Tooltip("How long a key must be pressed for before it is registered as being held down.")]
        [SerializeField] float LongPressDelay = 0.2f;

        [Tooltip("Interval between retriggers when holding a key down.")]
        [SerializeField] float RetriggerDelay = 0.05f;

        [SerializeField]
        KeyBinding[] KeyBindings = {
            new KeyBinding(Modifier.None,    KeyCode.F2,         Command.ToggleOpen      ),
            new KeyBinding(Modifier.None,    KeyCode.UpArrow,    Command.Up              ),
            new KeyBinding(Modifier.None,    KeyCode.DownArrow,  Command.Down            ),
            new KeyBinding(Modifier.None,    KeyCode.LeftArrow,  Command.Left            ),
            new KeyBinding(Modifier.None,    KeyCode.RightArrow, Command.Right           ),
            new KeyBinding(Modifier.None,    KeyCode.I,          Command.Up              ),
            new KeyBinding(Modifier.None,    KeyCode.K,          Command.Down            ),
            new KeyBinding(Modifier.None,    KeyCode.J,          Command.Left            ),
            new KeyBinding(Modifier.None,    KeyCode.L,          Command.Right           ),
            new KeyBinding(Modifier.None,    KeyCode.R,          Command.Rotate          ),
            new KeyBinding(Modifier.None,    KeyCode.S,          Command.SelectItem      ),
            new KeyBinding(Modifier.Control, KeyCode.A,          Command.SelectSimilar   ),
            new KeyBinding(Modifier.None,    KeyCode.A,          Command.SelectAll       ),
            new KeyBinding(Modifier.Alt,     KeyCode.S,          Command.DeselectItem    ),
            new KeyBinding(Modifier.Alt,     KeyCode.A,          Command.DeselectAll     ),
            new KeyBinding(Modifier.None,    KeyCode.D,          Command.RandomDeselect  ),
            new KeyBinding(Modifier.Shift,   KeyCode.D,          Command.RandomInc       ),
            new KeyBinding(Modifier.Alt,     KeyCode.D,          Command.RandomDec       ),
            new KeyBinding(Modifier.None,    KeyCode.V,          Command.CreateVertex    ),
            new KeyBinding(Modifier.None,    KeyCode.G,          Command.ToggleGrab      ),
            new KeyBinding(Modifier.Shift,   KeyCode.C,          Command.ToggleCamera    ),
            new KeyBinding(Modifier.None,    KeyCode.B,          Command.ToggleBoxSelect ),
            new KeyBinding(Modifier.None,    KeyCode.P,          Command.TogglePalette   ),
            new KeyBinding(Modifier.Alt,     KeyCode.V,          Command.ToggleLineSelect),
            new KeyBinding(Modifier.Shift,   KeyCode.V,          Command.FlipVertical    ),
            new KeyBinding(Modifier.Shift,   KeyCode.H,          Command.FlipHorizontal  ),
            new KeyBinding(Modifier.None,    KeyCode.Escape,     Command.NormalMode      ),
            new KeyBinding(Modifier.None,    KeyCode.C,          Command.Clone           ),
            new KeyBinding(Modifier.None,    KeyCode.W,          Command.Write           ),
            new KeyBinding(Modifier.None,    KeyCode.U,          Command.Undo            ),
            new KeyBinding(Modifier.None,    KeyCode.E,          Command.Erase           ),
            new KeyBinding(Modifier.None,    KeyCode.F,          Command.FocusView       ),
            new KeyBinding(Modifier.Control, KeyCode.N,          Command.NewLayer        ),
            new KeyBinding(Modifier.Control, KeyCode.I,          Command.LayerZInc       ),
            new KeyBinding(Modifier.Control, KeyCode.K,          Command.LayerZDec       ),
            new KeyBinding(Modifier.Control, KeyCode.UpArrow,    Command.LayerZInc       ),
            new KeyBinding(Modifier.Control, KeyCode.DownArrow,  Command.LayerZDec       ),
            new KeyBinding(Modifier.Control, KeyCode.RightArrow, Command.NextModel       ),
            new KeyBinding(Modifier.Control, KeyCode.LeftArrow,  Command.PreviousModel   ),
            new KeyBinding(Modifier.Control, KeyCode.UpArrow,    Command.NextLayer       ),
            new KeyBinding(Modifier.Control, KeyCode.DownArrow,  Command.PreviousLayer   ),
            new KeyBinding(Modifier.Control, KeyCode.L,          Command.NextModel       ),
            new KeyBinding(Modifier.Control, KeyCode.J,          Command.PreviousModel   ),
            new KeyBinding(Modifier.Control, KeyCode.I,          Command.NextLayer       ),
            new KeyBinding(Modifier.Control, KeyCode.K,          Command.PreviousLayer   ),
            new KeyBinding(Modifier.Control, KeyCode.W,          Command.WriteLvl        ),
            new KeyBinding(Modifier.Control, KeyCode.O,          Command.OpenLvl         ),
            new KeyBinding(Modifier.Control, KeyCode.Equals,     Command.ZoomIn          ),
            new KeyBinding(Modifier.Control, KeyCode.Plus,       Command.ZoomIn          ),
            new KeyBinding(Modifier.Control, KeyCode.Minus,      Command.ZoomOut         ),
            new KeyBinding(Modifier.None,    KeyCode.M,          Command.MacroPlay       ),
            new KeyBinding(Modifier.Control, KeyCode.M,          Command.MacroStart      ),
            new KeyBinding(Modifier.Alt,     KeyCode.M,          Command.MacroStop       ),
        };

        float long_press_time;
        float retrigger_time;
        KeyBinding long_press_key;
        Modifier modifier = Modifier.None;

        internal Command HandleKeyPress() {
            long_press_time += Time.deltaTime;

            for (int i = 0; i < valid_keys.Length; i++) {
                var key = valid_keys[i];

                if (Input.GetKeyUp(key)) {
                    HandleModifierUp(key);
                    long_press_time = 0;
                    long_press_key = null_key;
                    continue;
                }

                if (Input.GetKeyDown(key)) {
                    HandleModifierDown(key);
                    long_press_time = 0;
                    return MapKeyPress(modifier, key);
                }

                if (Input.GetKey(key) && long_press_time >= LongPressDelay) {
                    // The delay prevents spamming a key when it is pressed and
                    // released, but allows keys that have been held down for
                    // some time to issue multiple commands.
                    //
                    // @Todo: Some shortcuts should not allow for this feature

                    if (retrigger_time >= RetriggerDelay) {
                        retrigger_time = 0;
                        return long_press_key.command;
                    }

                    retrigger_time += Time.deltaTime;

                    if (key != long_press_key.keycode || modifier != long_press_key.modifier) {
                        MapKeyPress(modifier, key);
                    }
                }
            }

            return Command.Nop;
        }

        void HandleModifierUp(KeyCode key) {
            switch (key) {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    modifier = Modifier.None;
                    break;
            }
        }

        void HandleModifierDown(KeyCode key) {
            switch (key) {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftCommand:
                case KeyCode.RightCommand:
                    modifier = Modifier.Control;
                    break;

                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    modifier = Modifier.Shift;
                    break;

                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                    modifier = Modifier.Alt;
                    break;
            }
        }

        Command MapKeyPress(Modifier mod, KeyCode key) {
            for (int i = 0; i < KeyBindings.Length; i++) {
                var key_binding = KeyBindings[i];

                if (key_binding.keycode == key && key_binding.modifier == mod) {
                    long_press_key = key_binding;
                    return key_binding.command;
                }
            }
            return Command.Nop;
        }
    }
}
