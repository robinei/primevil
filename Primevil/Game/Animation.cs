
namespace Primevil.Game
{
    class Animation
    {
        private readonly Sprite sprite;
        private readonly float frameDelay;
        private float timer;
        private int index;
        private Direction direction;

        public Animation(Sprite sprite, float frameDelay)
        {
            this.sprite = sprite;
            this.frameDelay = frameDelay;
        }

        public Sprite Sprite { get { return sprite; } }

        public Direction Direction
        {
            get { return direction; }
            set
            {
                direction = value;
                index = index % sprite.GetFrameCount(direction);
            }
        }

        public void Update(float dt)
        {
            timer += dt;
            if (timer >= frameDelay) {
                timer -= frameDelay;
                index = (index + 1) % sprite.GetFrameCount(direction);
            }
        }

        public Sprite.Frame CurrentFrame
        {
            get { return sprite.GetFrame(direction, index); }
        }
    }
}
