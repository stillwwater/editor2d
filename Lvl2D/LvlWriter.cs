using System;
using System.IO;
using UnityEngine;

namespace Lvl2D
{
    public class LvlWriter
    {
        enum State
        {
            Header,
            Layer,
            Entity
        }

        BinaryWriter writer;
        State state;
        int entity_count;

        public LvlWriter(BinaryWriter writer) {
            this.writer = writer;
            state = State.Header;
        }

        public bool WriteHeader(LvlHeader header) {
            if (!ValidState(State.Header))
                return false;

            writer.Write(LvlHeader.SIGNATURE_VERSION);
            writer.Write(header.layers);
            writer.Write(header.palette);
            writer.Write(header.width);
            writer.Write(header.height);
            writer.Write(header.camera.x);
            writer.Write(header.camera.y);
            writer.Write(header.camera.z);
            writer.Write(header.camera.ortho_size);
            writer.Write(header.camera.set_color);
            writer.Write(header.camera.r);
            writer.Write(header.camera.g);
            writer.Write(header.camera.b);
            writer.Write(header.name);
            writer.Write(header.author);
            state = State.Layer;
            return true;
        }

        public bool WriteLayer(LvlLayer layer) {
            if (!ValidState(State.Layer))
                return false;

            writer.Write(layer.layer_id);
            writer.Write(layer.z_depth);
            state = State.Entity;
            entity_count = 0;
            return true;
        }

        ///
        /// Write an entity to the .lvl file.
        ///
        /// Bit format:
        /// hvrrpppp pppppppp
        ///
        /// h: horizontal-flip
        /// v: vertical-flip
        /// r: rotation (0b00: 0, 0b01: 90, 0b10: 180, 0b11: 270)
        /// p: palette index (prefab entry) (12-bit integer)
        ///
        /// * Rotation is counter-clockwise on the z-axis
        ///
        public bool WriteEntity(GameObject entity, int palette_index, int map_size) {
            if (!ValidState(State.Entity))
                return false;

            if (++entity_count == map_size)
                state = State.Layer;

            if (!entity) {
                writer.Write((ushort)0);
                return true;
            }

            var sprite = entity.GetComponent<SpriteRenderer>();
            if (!sprite) {
                string name = entity.name;
                Debug.LogErrorFormat("[lvl2d] {0} is missing a sprite renderer.", name);
                return false;
            }

            byte h = (byte)(sprite.flipX ? 1 : 0);
            byte v = (byte)(sprite.flipY ? 1 : 0);
            byte r = MapRotation(entity.transform.eulerAngles.z);

            // Palette index is 1 based since 0 denotes an empty cell
            Debug.Assert(palette_index < 0xFFF);
            int low  = (palette_index + 1) & 0x00000FFF;
            int high = (h << 15) | (v << 14) | (r << 12);
            writer.Write((ushort)(high | low));

            return true;
        }

        public bool WriteAll(
            LvlHeader header,
            Func<int, LvlLayer> layer_callback,
            Func<int, Vector2Int, GameObject> entity_callback,
            Func<GameObject, int> find_palette_index_callback)
        {
            if (!WriteHeader(header))
                return false;

            int map_size = header.width * header.height;

            for (int layer = 0; layer < header.layers; layer++) {
                if (!WriteLayer(layer_callback(layer)))
                    return false;

                for (int i = 0; i < header.width; i++) {
                    for (int j = 0; j < header.height; j++) {
                        var entity = entity_callback(layer, new Vector2Int(i, j));
                        int palette_index;

                        if (entity)
                            palette_index = find_palette_index_callback(entity);
                        else
                            palette_index = -1;

                        if (!WriteEntity(entity, palette_index, map_size))
                            return false;
                    }
                }
            }
            return true;
        }

        byte MapRotation(float rotation) {
            switch (Mathf.RoundToInt(rotation) % 360) {
                case 270:
                case -90:
                    return 0b11;

                case 180:
                case -180:
                    return 0b10;

                case 90:
                case -270:
                    return 0b01;

                case 0:
                    return 0;

                default:
                    Debug.LogWarningFormat(
                        "[lvl2d] Invalid rotation {0}. Assuming 0.",
                        rotation);
                    return 0;
            }
        }

        bool ValidState(State received) {
            if (state == received) {
                return true;
            }
            Debug.LogErrorFormat("[lvl2d] Expected '{0}', received '{1}'.", state, received);
            return false;
        }
    }
}
