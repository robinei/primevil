using System;

namespace Primevil.Formats
{
    class MINFile
    {
        private readonly int pillarSize;
        private readonly byte[] data;

        public MINFile(byte[] data, string path)
        {
            this.data = data;

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
            get { return data.Length / (pillarSize * 2); }
        }

        public int GetCelIndex(int minIndex, int x, int y) // x is [0, 1], y is [0, 5 or 8]
        {
            int i = y * 2 + x;
            int offset = minIndex * pillarSize * 2 + i * 2;
            int val = BitConverter.ToInt16(data, offset);
            return (val & 0x0FFF) - 1;
        }
    }
}
