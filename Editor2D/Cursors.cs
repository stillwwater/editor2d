using System;
using System.Collections.Generic;
using UnityEngine;

namespace Editor2D
{
    internal struct Cursor
    {
        internal Vector3 position;
        internal bool pinned;
    }

    ///
    /// Data structure for handling buffer cursors.
    /// Speeds up detection of cursors that share the
    /// same position on the grid.
    ///
    internal class Cursors
    {
        readonly List<Cursor> data;
        readonly Dictionary<ulong, int> duplicates;
        readonly float cell_scale;
        readonly Rect bounds;

        bool synced;

        internal Cursor this[int index] {
            get => data[index];
            set {
                Debug.Assert(synced);
                ulong old_id = EncodePosition(data[index].position);
                ulong new_id = EncodePosition(value.position);

                duplicates[old_id]--;

                if (duplicates.ContainsKey(new_id))
                    duplicates[new_id]++;
                else
                    duplicates.Add(new_id, 0);

                if (duplicates[old_id] < 0)
                    duplicates.Remove(old_id);

                data[index] = value;
            }
        }

        internal int Count {
            get => data.Count;
        }

        internal Cursors(Rect bounds, float cell_scale) {
            this.bounds = bounds;
            this.cell_scale = cell_scale;
            data = new List<Cursor>(1);
            duplicates = new Dictionary<ulong, int>();
            Add(Vector3.zero); // @Todo: Camera position
            synced = true;
        }

        internal void SetUnchecked(int index, Cursor value) {
            data[index] = value;
            synced = false;
        }

        internal void Clear() {
            data.Clear();
            duplicates.Clear();
            synced = true;
        }

        internal void Reserve(int size) {
            data.Capacity = Math.Max(data.Capacity, size);
        }

        /// Check if there are multiple cursors in a given position
        internal bool IsDuplicate(Vector3 position) {
            ulong id = EncodePosition(position);
            if (duplicates.TryGetValue(id, out int num_duplicates)) {
                return num_duplicates > 0;
            }
            return false;
        }

        internal bool RemoveDuplicate(Vector3 position, int ignore_index = -1) {
            ulong id = EncodePosition(position);
            bool removed = false;
            for (int i = 0; i < data.Count; i++) {
                if (i != ignore_index && data[i].position == position) {
                    data.RemoveAt(i);
                    duplicates[id]--;
                    removed = true;
                }
            }
            return removed;
        }

        /// Check if a cursor exists at a World position
        internal bool Contains(Vector3 position) {
            ulong id = EncodePosition(position);
            return duplicates.ContainsKey(id);
        }

        internal bool Add(Cursor cursor) {
            data.Add(cursor);
            return AddToDuplicates(cursor);
        }

        internal bool Add(Vector3 position, bool pinned = false) {
            return Add(new Cursor() {
                position = position,
                pinned   = pinned
            });
        }

        ///
        /// Add cursor without adding checking if it already exists.
        /// Cursors.Sync must be called before checking for duplicates.
        ///
        internal void AddUnchecked(Vector3 position, bool pinned = false) {
            data.Add(new Cursor() {
                position = position,
                pinned   = pinned
            });
            synced = false;
        }

        /// Build duplicates dictionary if Cursors is out of sync.
        internal void Sync() {
            if (synced)
                return;

            duplicates.Clear();
            foreach (var cursor in data) {
                AddToDuplicates(cursor);
            }
            synced = true;
        }

        bool AddToDuplicates(Cursor cursor) {
            ulong id = EncodePosition(cursor.position);
            bool is_dup = duplicates.ContainsKey(id);

            if (is_dup)
                duplicates[id]++;
            else
                duplicates.Add(id, 0);

            return is_dup;
        }

        ulong EncodePosition(Vector3 position) {
            // Ignore z-axis since entities with different z values
            // must be on separate layers.
            ulong x = (ulong)((position.x - bounds.x) / cell_scale);
            ulong y = (ulong)((position.y - bounds.y) / cell_scale);

            // Maximum grid size of 2^64
            return x | (y << 32);
        }
    }
}
