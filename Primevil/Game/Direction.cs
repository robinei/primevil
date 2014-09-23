using System;

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

        private static readonly float D = (float)Math.Sqrt(2.0);
        private static readonly float[] Dist = { 1.0f, D, 1.0f, D, 1.0f, D, 1.0f, D };

        public static float StepDistance(this Direction dir)
        {
            return Dist[(int)dir];
        }

        public static Coord DeltaCoord(this Direction dir)
        {
            return new Coord(DeltaX[(int)dir], DeltaY[(int)dir]);
        }

        public static Direction Opposite(this Direction dir)
        {
            return (Direction)(((int)dir + 4) & 7);
        }
    }
}