namespace Primevil.Game
{
    public class Sprite
    {
        private object texture;
        private Rect[] rects;

        public Sprite(object texture, Rect[] rects)
        {
            this.texture = texture;
            this.rects = rects;
        }

        public Sprite(TextureAtlas atlas)
        {
            texture = atlas.Texture;
            rects = atlas.Rects;
        }

        public object Texture { get { return texture; } }

        public int FrameCount
        {
            get { return rects.Length / 8; }
        }

        public Rect GetRect(Direction direction, int index)
        {
            return rects[(int)direction * FrameCount + index];
        }
    }
}
