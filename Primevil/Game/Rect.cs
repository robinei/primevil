using System;

namespace Primevil.Game
{
    public struct Rect
    {
        public int X, Y, Width, Height;

        public Rect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
