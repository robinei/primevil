using System;

namespace Primevil.Formats
{
    class TILFile
    {
        private readonly short[] pillars;

        public struct Block
        {
            public short Top;
            public short Left;
            public short Right;
            public short Bottom;
        }

        public TILFile(byte[] data, int offset = 0, int size = 0)
        {
            if (size == 0)
                size = data.Length;
            pillars = new short[size / 2];
            Buffer.BlockCopy(data, offset, pillars, 0, size);
        }

        public Block GetBlock(int index)
        {
            int offset = index * 4;
            Block b;
            b.Top = pillars[offset + 0];
            b.Left = pillars[offset + 2];
            b.Right = pillars[offset + 1];
            b.Bottom = pillars[offset + 3];
            return b;
        }

        public int NumBlocks
        {
            get { return pillars.Length / 4; }
        }
    }
}
