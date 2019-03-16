using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor2D
{
    internal class Undo
    {
        internal struct State
        {
            internal GameObject entity;
            internal Vector3 position;
            internal Vector3 scale;
            internal Quaternion rotation;
            internal bool alive;
        }

        internal struct Frame
        {
            internal int layer;
            internal List<State> states;
            internal DateTime time;
        }

        readonly Frame[] stack;
        int stack_position = -1;

        internal Undo(int size = 256) {
            stack = new Frame[size];
        }

        ///
        /// Create a new frame in which states can be recorded.
        /// When an action is undone, all states in a single
        /// frame are undone at the same time.
        ///
        internal void PushFrame(int layer) {
            stack_position++;

            if (stack_position == stack.Length)
                stack_position = 0;

            var frame = new Frame() {
                layer  = layer,
                states = new List<State>(),
                time   = DateTime.Now
            };
            stack[stack_position] = frame;
        }

        internal bool PopFrame(out Frame frame) {
            if (stack_position < 0
                || stack_position > stack.Length
                || stack[stack_position].states == null) {
                frame = new Frame();
                return false;
            }

            frame = stack[stack_position];
            stack[stack_position--].states = null;

            if (stack_position < 0)
                // Wrap around
                stack_position = stack.Length - 1;

            return true;
        }

        ///
        /// Register an entity state that can be reverted back
        /// to in the future.
        ///
        internal void RegisterState(GameObject entity) {
            if (!entity) return;

            if (stack_position >= stack.Length || stack_position < 0) {
                Debug.LogError("[e2d] Cannot register state. Missing undo frame.");
                return;
            }

            stack[stack_position].states.Add(new State() {
                entity   = entity,
                position = entity.transform.position,
                scale    = entity.transform.localScale,
                rotation = entity.transform.rotation,
                alive    = entity.activeSelf
            });
        }

        internal void Clear() {
            for (int i = 0; i < stack.Length; i++)
                stack[i].states = null;
            stack_position = -1;
        }

        internal string TimeDelta(Frame frame) {
            int seconds = (int)((DateTime.Now - frame.time).TotalSeconds);
            if (seconds < 100) {
                string s = seconds == 1 ? "" : "s";
                return string.Format("{0} second{1} ago", seconds, s);
            }
            return frame.time.ToString("HH:mm:ss");
        }
    }
}
