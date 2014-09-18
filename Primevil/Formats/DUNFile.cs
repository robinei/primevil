using System;

namespace Primevil.Formats
{
    class DUNFile
    {
        private readonly byte[] data;
        private readonly int width;
        private readonly int height;

        public DUNFile(byte[] data)
        {
            this.data = data;
            width = BitConverter.ToInt16(data, 0);
            height = BitConverter.ToInt16(data, 2);
        }

        public int Width { get { return width; } }

        public int Height { get { return height; } }

        public int GetTileIndex(int x, int y)
        {
            int offset = y * width * 2 + x * 2 + 4;
            return BitConverter.ToInt16(data, offset);
        }
    }
}
