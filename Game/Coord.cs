using System;

namespace Primevil.Game
{
    public struct Coord
    {
        public int X;
        public int Y;

        public Coord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public CoordF ToCoordF()
        {
            return new CoordF(X, Y);
        }

        public static bool operator ==(Coord a, Coord b)
        {
            return (a.X == b.X) && (a.Y == b.Y);
        }

        public static bool operator !=(Coord a, Coord b)
        {
            return (a.X != b.X) || (a.Y != b.Y);
        }

        public bool Equals(Coord other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is Coord && Equals((Coord)obj);
        }

        public override int GetHashCode()
        {
            unchecked {
                return (X * 397) ^ Y;
            }
        }

        // intended to be used for adjacent coordinates
        public Direction DirectionTo(Coord c)
        {
            if (c.X < X) {
                if (c.Y < Y) return Direction.NorthWest;
                if (c.Y > Y) return Direction.SouthWest;
                return Direction.West;
            }
            if (c.X > X) {
                if (c.Y < Y) return Direction.NorthEast;
                if (c.Y > Y) return Direction.SouthEast;
                return Direction.East;
            }
            if (c.Y < Y) return Direction.North;
            if (c.Y > Y) return Direction.South;
            return Direction.North; // same coordinate, but we just say north
        }

        public static Coord operator +(Coord a, Coord b)
        {
            return new Coord(a.X + b.X, a.Y + b.Y);
        }

        public override string ToString()
        {
            return String.Format("Coord({0}, {1})", X, Y);
        }
    }


    public struct CoordF
    {
        public float X;
        public float Y;

        public CoordF(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Coord ToCoord()
        {
            return new Coord((int)X, (int)Y);
        }

        public float Length
        {
            get
            {
                var x = (double)X;
                var y = (double)Y;
                return (float)Math.Sqrt(x * x + y * y);
            }
        }

        public void Normalize()
        {
            float f = 1.0f / Length;
            X *= f;
            Y *= f;
        }

        public static CoordF operator +(CoordF a, CoordF b)
        {
            return new CoordF(a.X + b.X, a.Y + b.Y);
        }

        public static CoordF operator -(CoordF a, CoordF b)
        {
            return new CoordF(a.X - b.X, a.Y - b.Y);
        }

        public static CoordF operator *(CoordF a, float f)
        {
            return new CoordF(a.X * f, a.Y * f);
        }
    }
}