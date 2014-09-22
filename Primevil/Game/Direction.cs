namespace Primevil.Game
{
    public enum Direction
    {
        South,
        SouthWest,
        West,
        NorthWest,
        North,
        NorthEast,
        East,
        SouthEast
    }

    public static class DirectionExtensions
    {
        private static readonly int[] DeltaX = { 0, -1, -1, -1, 0, 1, 1, 1 };
        private static readonly int[] DeltaY = { 1, 1, 0, -1, -1, -1, 0, 1 };

        public static Coord DeltaCoord(this Direction dir)
        {
            return new Coord(DeltaX[(int)dir], DeltaY[(int)dir]);
        }
    }
}