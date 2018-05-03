namespace Primevil.Game
{
    public class Map
    {
        public readonly int Width;
        public readonly int Height;

        private readonly short[] pillars;
        private readonly byte[] flags;
        private readonly Creature[] creatures;

        private const int FlagPassable = 1;


        public Map(int width, int height)
        {
            Width = width;
            Height = height;

            int size = width * height;
            pillars = new short[size];
            flags = new byte[size];
            creatures = new Creature[size];

            for (int i = 0; i < size; ++i)
                pillars[i] = -1;
        }


        public void PlaceSector(SectorTemplate sector, int x, int y)
        {
            for (int j = 0; j < sector.Height; ++j) {
                for (int i = 0; i < sector.Width; ++i) {
                    var index = (y + j) * Width + x + i;
                    pillars[index] = (short)sector.GetPillar(i, j);
                    byte f = 0;
                    if (sector.IsPassable(i, j))
                        f |= FlagPassable;
                    flags[index] = f;
                }
            }
        }


        public int GetPillar(int x, int y) {
            return pillars[y * Width + x];
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

        public float GetCost(Coord pos, Direction fromDir)
        {
            return IsPassable(pos) ? 1.0f : float.MaxValue;
        }


        public void PlaceCreature(Creature c)
        {
            var pos = c.Position.ToCoord();
            c.MapCoord = pos;
            creatures[pos.Y * Width + pos.X] = c;
        }

        public Creature GetCreature(Coord pos)
        {
            int index = pos.Y * Width + pos.X;
            var c = creatures[index];
            if (c != null) {
                if (c.MapCoord == pos)
                    return c;
                creatures[index] = null;
            }
            return null;
        }
    }
}

