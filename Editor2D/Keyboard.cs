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
            NONE,
            CONTROL,
            SHIFT,
            ALT
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
        static KeyBinding null_key  = new KeyBinding(Modifier.NONE, KeyCode.None, Command.NOP);

        [Tooltip("How long a key must be pressed for before it is registered as being held down.")]
        [SerializeField] float LongPressDelay = 0.2f;

        [Tooltip("Interval between retriggers when holding a key down.")]
        [SerializeField] float RetriggerDelay = 0.1f;

        [SerializeField]
        KeyBinding[] KeyBindings = {
            new KeyBinding(Modifier.NONE,    KeyCode.UpArrow,    Command.TRANSFORM_UP          ),
            new KeyBinding(Modifier.NONE,    KeyCode.DownArrow,  Command.TRANSFORM_DOWN        ),
            new KeyBinding(Modifier.NONE,    KeyCode.LeftArrow,  Command.TRANSFORM_LEFT        ),
            new KeyBinding(Modifier.NONE,    KeyCode.RightArrow, Command.TRANSFORM_RIGHT       ),
            new KeyBinding(Modifier.NONE,    KeyCode.I,          Command.TRANSFORM_UP          ),
            new KeyBinding(Modifier.NONE,    KeyCode.K,          Command.TRANSFORM_DOWN        ),
            new KeyBinding(Modifier.NONE,    KeyCode.J,          Command.TRANSFORM_LEFT        ),
            new KeyBinding(Modifier.NONE,    KeyCode.L,          Command.TRANSFORM_RIGHT       ),
            new KeyBinding(Modifier.NONE,    KeyCode.A,          Command.TOGGLE_SIMILAR        ),
            new KeyBinding(Modifier.CONTROL, KeyCode.A,          Command.TOGGLE_ALL            ),
            new KeyBinding(Modifier.SHIFT,   KeyCode.S,          Command.TOGGLE_SCALE          ),
            new KeyBinding(Modifier.NONE,    KeyCode.G,          Command.TOGGLE_GRAB           ),
            new KeyBinding(Modifier.NONE,    KeyCode.B,          Command.TOGGLE_BOX_SELECT     ),
            new KeyBinding(Modifier.NONE,    KeyCode.R,          Command.TOGGLE_ROTATE         ),
            new KeyBinding(Modifier.SHIFT,   KeyCode.S,          Command.TOGGLE_SCALE          ),
            new KeyBinding(Modifier.NONE,    KeyCode.S,          Command.SELECT_ITEM           ),
            new KeyBinding(Modifier.NONE,    KeyCode.Escape,     Command.NORMAL_MODE           ),
            new KeyBinding(Modifier.NONE,    KeyCode.C,          Command.CLONE                 ),
            new KeyBinding(Modifier.NONE,    KeyCode.W,          Command.WRITE                 ),
            new KeyBinding(Modifier.CONTROL, KeyCode.X,          Command.CUT                   ),
            new KeyBinding(Modifier.CONTROL, KeyCode.C,          Command.COPY                  ),
            new KeyBinding(Modifier.CONTROL, KeyCode.V,          Command.PASTE                 ),
            new KeyBinding(Modifier.CONTROL, KeyCode.Z,          Command.UNDO                  ),
            new KeyBinding(Modifier.CONTROL, KeyCode.Y,          Command.REDO                  ),
            new KeyBinding(Modifier.NONE,    KeyCode.X,          Command.DELETE                ),
            new KeyBinding(Modifier.SHIFT,   KeyCode.F,          Command.FOCUS_VIEW            ),
            new KeyBinding(Modifier.CONTROL, KeyCode.N,          Command.NEW_LAYER             ),
            new KeyBinding(Modifier.CONTROL, KeyCode.RightArrow, Command.NEXT_MODEL            ),
            new KeyBinding(Modifier.CONTROL, KeyCode.LeftArrow,  Command.PREVIOUS_MODEL        ),
            new KeyBinding(Modifier.CONTROL, KeyCode.UpArrow,    Command.NEXT_LAYER            ),
            new KeyBinding(Modifier.CONTROL, KeyCode.DownArrow,  Command.PREVIOUS_LAYER        ),
            new KeyBinding(Modifier.SHIFT,   KeyCode.UpArrow,    Command.MOVE_NEXT_LAYER       ),
            new KeyBinding(Modifier.SHIFT,   KeyCode.DownArrow,  Command.MOVE_PREVIOUS_LAYER   ),
        };

        float long_press_time;
        float retrigger_time;
        KeyBinding long_press_key;
        Modifier modifier = Modifier.NONE;

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

            return Command.NOP;
        }

        Command MapKeyPress(Modifier mod, KeyCode key) {
            for (int i = 0; i < KeyBindings.Length; i++) {
                var key_binding = KeyBindings[i];

                if (key_binding.keycode == key && key_binding.modifier == mod) {
                    long_press_key = key_binding;
                    return key_binding.command;
                }
            }
            return Command.NOP;
        }

        void HandleModifierUp(KeyCode key) {
            switch (key) {
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: {
                    modifier = Modifier.NONE;
                    break;
                }
            }
        }

        void HandleModifierDown(KeyCode key) {
            switch (key) {
                case KeyCode.LeftControl:
                case KeyCode.RightControl: {
                    modifier = Modifier.CONTROL;
                    break;
                }
                
                case KeyCode.LeftShift:
                case KeyCode.RightShift: {
                    modifier = Modifier.SHIFT;
                    break;
                }

                case KeyCode.LeftAlt:
                case KeyCode.RightAlt: {
                    modifier = Modifier.ALT;
                    break;
                }
            }
        }
    }
}
