using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor2D
{
    internal struct UndoState
    {
        internal GameObject entity;
        internal Vector3 position;
        internal Vector3 scale;
        internal Quaternion rotation;
        internal int layer;
        internal bool alive;
    }

    internal struct UndoFrame
    {
        internal int layer;
        internal List<UndoState> states;
    }

    internal class Undo
    {
        const int UNDO_SIZE = 1024;
        readonly UndoFrame[] stack = new UndoFrame[UNDO_SIZE];
        int stack_position = -1;

        internal void RegisterChange(GameObject entity, int layer) {
            if (!entity) return;
            stack[stack_position].states.Add(new UndoState() {
                entity   = entity,
                position = entity.transform.position,
                scale    = entity.transform.localScale,
                rotation = entity.transform.rotation,
                alive    = entity.active,
                layer    = layer,
            });
        }

        internal void PushFrame(Buffer buffer) {
            stack_position++;

            if (stack_position == stack.Length)
                stack_position = 0;

            Debug.Log(stack_position);

            var frame = new UndoFrame() {
                layer  = buffer.layer,
                states = new List<UndoState>()
            };
            stack[stack_position] = frame;
        }

        internal bool PopFrame(out UndoFrame frame) {
            if (stack_position < 0) {
                frame = new UndoFrame();
                return false;
            }
            frame = stack[stack_position--];
            return true;
        }
    }
}
