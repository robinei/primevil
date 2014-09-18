using System;

namespace Primevil.Formats
{
    class TILFile
    {
        private readonly byte[] data;

        public struct Block
        {
            public short Top;
            public short Left;
            public short Right;
            public short Bottom;
        }

        public TILFile(byte[] data)
        {
            this.data = data;
        }

        public Block GetBlock(int index)
        {
            int offset = index * 8;
            Block b;
            b.Top = BitConverter.ToInt16(data, offset + 0);
            b.Right = BitConverter.ToInt16(data, offset + 2);
            b.Left = BitConverter.ToInt16(data, offset + 4);
            b.Bottom = BitConverter.ToInt16(data, offset + 6);
            return b;
        }

        public int NumBlocks
        {
            get { return data.Length / 8; }
        }
    }
}
