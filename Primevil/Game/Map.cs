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
        private readonly byte[] flags;

        private const int FlagPassable = 1;

        public Map(int width, int height)
        {
            Width = width;
            Height = height;

            int size = width * height;
            cells = new Cell[size];
            flags = new byte[size];
            for (int i = 0; i < size; ++i) {
                cells[i].Pillar = -1;
                flags[i] = 0;
            }
        }

        public void PlaceSector(SectorTemplate sector, int x, int y)
        {
            for (int j = 0; j < sector.Height; ++j) {
                for (int i = 0; i < sector.Width; ++i) {
                    var index = (y + j) * Width + x + i;
                    cells[index].Pillar = sector.GetPillar(i, j);
                    byte f = 0;
                    if (sector.IsPassable(i, j))
                        f |= FlagPassable;
                    flags[index] = f;
                }
            }
        }

        public int GetPillar(int x, int y) {
            return cells[y * Width + x].Pillar;
        }

        public bool IsPassable(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return false;
            return (flags[y * Width + x] & FlagPassable) != 0;
        }

        public bool IsPassable(Coord c)
        {
            return IsPassable(c.X, c.Y);
        }
    }
}

