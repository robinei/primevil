using System;

namespace Primevil.Formats
{
    public class BinaryReader
    {
        private readonly byte[] data;
        private int offset;

        public BinaryReader(byte[] data, int offset = 0)
        {
            this.data = data;
            this.offset = offset;
        }

        public void Skip(int count)
        {
            offset += count;
        }

        public ushort ReadU16()
        {
            var val = BitConverter.ToUInt16(data, offset);
            offset += 2;
            return val;
        }

        public uint ReadU32()
        {
            var val = BitConverter.ToUInt32(data, offset);
            offset += 4;
            return val;
        }
    }
}
