
namespace Primevil.Game
{
    public class Sprite
    {
        public struct Frame
        {
            public object Texture;
            public Rect SourceRect;

            public int Width { get { return SourceRect.Width; } }
            public int Height { get { return SourceRect.Height; } }
        }

        private readonly int[] frameCounts;
        private readonly Frame[] frames;

        public Sprite(int[] frameCounts, Frame[] frames)
        {
            this.frameCounts = frameCounts;
            this.frames = frames;
        }

        public int DirectionCount
        {
            get { return frameCounts.Length; }
        }

        public int GetFrameCount(Direction direction)
        {
            return frameCounts[(int)direction];
        }

        public Frame GetFrame(Direction direction, int index)
        {
            int offset = 0;
            for (int i = 0; i < (int)direction; ++i)
                offset += frameCounts[i];
            return frames[offset + index];
        }
    }
}
