
///
/// Compact binary format for serializing tile map data
///
namespace Lvl2D
{
    public struct LvlEntity
    {
        public bool empty;
        public int palette_index;
        public float rotation;
        public bool flip_x;
        public bool flip_y;
    }

    public struct LvlLayer
    {
        public int layer_id;
        public float z_depth;
    }

    public struct LvlHeader
    {
        public const int SIGNATURE = 0x4C564C;
        public const byte VERSION = 1;
        public const int SIGNATURE_VERSION = SIGNATURE | (VERSION << 24);
        public ushort layers;
        public ushort palette;
        public ushort width;
        public ushort height;
        public Camera camera;
        public string name;
        public string author;

        public struct Camera
        {
            public float x, y, z;
            public float ortho_size;
            public byte r, g, b;
            public bool set_color;
        }
    }
}
