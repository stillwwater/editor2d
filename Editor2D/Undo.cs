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
        }

        const int UNDO_SIZE = 1024;
        readonly Frame[] stack = new Frame[UNDO_SIZE];
        int stack_position = -1;

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
                alive    = entity.active
            });
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
                states = new List<State>()
            };
            stack[stack_position] = frame;
        }

        internal bool PopFrame(out Frame frame) {
            if (stack_position < 0) {
                frame = new Frame();
                Debug.Log("[e2d] No changes left to undo.");
                return false;
            }
            frame = stack[stack_position--];
            return true;
        }
    }
}
