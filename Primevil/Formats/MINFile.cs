using System;

namespace Primevil.Formats
{
    class MINFile
    {
        private readonly int pillarSize;
        private readonly short[] indexes;

        public MINFile(string path, byte[] data, int offset = 0, int size = 0)
        {
            if (size == 0)
                size = data.Length;
            indexes = new short[size / 2];
            Buffer.BlockCopy(data, offset, indexes, 0, size);

            if (path.EndsWith("town.min") || path.EndsWith("l4.min"))
                pillarSize = 16;
            else
                pillarSize = 10;
        }

        public int PillarHeight
        {
            get { return pillarSize / 2; }
        }

        public int NumPillars
        {
            get { return indexes.Length / pillarSize; }
        }

        public int GetCelIndex(int minIndex, int x, int y) // x is [0, 1], y is [0, 5 or 8]
        {
            int offset = minIndex * pillarSize + y * 2 + x;
            int val = indexes[offset];
            return (val & 0x0FFF) - 1;
        }
    }
}
