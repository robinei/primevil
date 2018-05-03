
namespace Primevil.Game
{
    public class Animation
    {
        private float timer;
        private int index;

        public readonly Sprite Sprite;
        public readonly float FrameDelay;
        public Direction Direction;
        public object Texture { get { return Sprite.Texture; } }

        public Animation(Sprite sprite, float frameDelay)
        {
            Sprite = sprite;
            FrameDelay = frameDelay;
        }

        public Rect CurrentRect
        {
            get { return Sprite.GetRect(Direction, index); }
        }

        public void Reset()
        {
            timer = 0.0f;
            index = 0;
        }

        public void Update(float dt)
        {
            timer += dt;
            if (timer >= FrameDelay) {
                timer -= FrameDelay;
                index = (index + 1) % Sprite.FrameCount;
            }
        }
    }
}
