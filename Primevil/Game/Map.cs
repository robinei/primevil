using System;

namespace Primevil.Game
{
    public class Map
    {
        private struct Cell
        {
            public int Pillar;
        }

        public readonly int Width;
        public readonly int Height;

        private readonly Cell[] cells;

        public Map(int width, int height)
        {
            Width = width;
            Height = height;

            int size = width * height;
            cells = new Cell[size];
            for (int i = 0; i < size; ++i) {
                cells[i].Pillar = -1;
            }
        }

        public void PlaceSector(SectorTemplate sector, int x, int y)
        {
            for (int j = 0; j < sector.Height; ++j) {
                for (int i = 0; i < sector.Width; ++i) {
                    cells[(y + j) * Width + x + i].Pillar = sector.GetPillar(i, j);
                }
            }
        }

        public int GetPillar(int x, int y) {
            return cells[y * Width + x].Pillar;
        }
    }
}

