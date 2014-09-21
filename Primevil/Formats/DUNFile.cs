using System;
using System.Diagnostics;

namespace Primevil.Formats
{
    public class DUNFile
    {
        private readonly int width;
        private readonly int height;
        private readonly short[] tiles;

        public DUNFile(byte[] data, int offset = 0)
        {
            width = BitConverter.ToInt16(data, offset);
            height = BitConverter.ToInt16(data, offset + 2);
            tiles = new short[width * height];
            Buffer.BlockCopy(data, 4, tiles, 0, width * height * 2);
        }

        public static DUNFile Load(MPQArchive mpq, string path)
        {
            using (var f = mpq.Open(path)) {
                var data = new byte[f.Length];
                var len = f.Read(data, 0, (int)f.Length);
                Debug.Assert(len == f.Length);
                return new DUNFile(data);
            }
        }

        public int Width { get { return width; } }

        public int Height { get { return height; } }

        public int GetTileIndex(int x, int y)
        {
            int index = y * width + x;
            return tiles[index] - 1;
        }
    }
}
