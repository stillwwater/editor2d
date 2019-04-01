using System;
using System.IO;
using UnityEngine;

namespace Lvl2D
{
    public class LvlReader
    {
        BinaryReader reader;

        public LvlReader(BinaryReader reader) {
            this.reader = reader;
        }

        public bool TryReadHeader(out LvlHeader header) {
            header = new LvlHeader();
            int sign = reader.ReadInt32();

            if (sign != (LvlHeader.SIGNATURE_VERSION)) {
                Debug.LogErrorFormat("[lvl2d] Incompatible version {0:X8}", sign);
                return false;
            }

            header.layers  = reader.ReadUInt16();
            header.palette = reader.ReadUInt16();
            header.width   = reader.ReadUInt16();
            header.height  = reader.ReadUInt16();
            header.camera = new LvlHeader.Camera() {
                x = reader.ReadSingle(),
                y = reader.ReadSingle(),
                z = reader.ReadSingle(),
                ortho_size = reader.ReadSingle(),
                r = reader.ReadByte(),
                g = reader.ReadByte(),
                b = reader.ReadByte(),
                set_color = reader.ReadBoolean()
            };
            header.name   = reader.ReadString();
            header.author = reader.ReadString();

            return true;
        }

        public LvlLayer ReadLayer() {
            return new LvlLayer() {
                layer_id = reader.ReadInt32(),
                z_depth  = reader.ReadSingle()
            };
        }

        public LvlEntity ReadEntity() {
            ushort cell = reader.ReadUInt16();

            if (cell == 0)
                return new LvlEntity() { empty = true };

            int h = (cell >> 15);
            int v = (cell >> 14) & 0b01;
            int r = (cell >> 12) & 0b0011;
            int palette_index = cell & 0x0FFF;

            return new LvlEntity() {
                empty = false,
                palette_index = palette_index - 1,
                rotation = MapRotation((byte)r),
                flip_x = h == 1,
                flip_y = v == 1
            };
        }

        ///
        /// Read entity data from all layers in a .lvl file.
        /// Callbacks are used so that the caller can handle the creation
        /// of new layers and instantiate new game objects using the entity
        /// data from the file.
        ///
        public bool ReadAll(
            Action<LvlLayer> create_layer_callback,
            Action<LvlEntity, Vector2Int> create_entity_callback,
            out LvlHeader header)
        {
            if (!TryReadHeader(out header))
                return false;

            for (int layer = 0; layer < header.layers; layer++) {
                create_layer_callback(ReadLayer());

                for (int i = 0; i < header.width; i++) {
                    for (int j = 0; j < header.height; j++) {
                        var pos = new Vector2Int(i, j);
                        var entity = ReadEntity();

                        if (entity.empty)
                            // Don't invoke callback unless there is data
                            continue;

                        create_entity_callback(entity, pos);
                    }
                }
            }
            return true;
        }

        float MapRotation(byte rotation) {
            switch (rotation) {
                case 0b00: return 0;
                case 0b01: return 90;
                case 0b10: return 180;
                case 0b11: return 270;
                default:
                    Debug.LogErrorFormat(
                        "[lvl2d] Invalid rotation bit pattern {0}.",
                        rotation);
                    return 0;
            }
        }
    }
}
