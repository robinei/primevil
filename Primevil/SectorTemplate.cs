using System;
using Primevil.Formats;

namespace Primevil
{
    public class SectorTemplate
    {
        public readonly int Width;
        public readonly int Height;

        private int[] data;

        public SectorTemplate(DUNFile dun, TILFile til)
        {
            Width = dun.Width * 2;
            Height = dun.Height * 2;
            data = new int[Width * Height];

            for (int j = 0; j < dun.Height; ++j) {
                for (int i = 0; i < dun.Width; ++i) {
                    int line0 = j * Width * 2 + i * 2;
                    int line1 = line0 + Width;

                    var bi = dun.GetTileIndex(i, j);
                    if (bi < 0) {
                        data[line0 + 0] = -1;
                        data[line0 + 1] = -1;
                        data[line1 + 0] = -1;
                        data[line1 + 1] = -1;
                        continue;
                    }

                    var b = til.GetBlock(bi);
                    data[line0 + 0] = b.Top;
                    data[line0 + 1] = b.Right;
                    data[line1 + 0] = b.Left;
                    data[line1 + 1] = b.Bottom;
                }
            }
        }

        public int GetPillar(int x, int y)
        {
            return data[y * Width + x];
        }
    }
}

