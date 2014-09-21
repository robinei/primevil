using System;
using System.Diagnostics;

namespace Primevil.Formats
{
    public class TILFile
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

        public static TILFile Load(MPQArchive mpq, string path)
        {
            using (var f = mpq.Open(path)) {
                var data = new byte[f.Length];
                var len = f.Read(data, 0, (int)f.Length);
                Debug.Assert(len == f.Length);
                return new TILFile(data);
            }
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
