namespace Primevil.Game
{
    public struct LevelCreatureList { }

    public class Creature : IIntrusiveMember<LevelCreatureList>
    {
        public IntrusiveLink<LevelCreatureList> LevelListLink;
        public void WithLink(IntrusiveLink<LevelCreatureList>.Handler func) { func(ref LevelListLink); }


        public Coord MapCoord;
        public CoordF Position;
        public Coord Target;
        public bool Walking;

        public Animation CurrentAnimation;


        public void Walk(Coord target)
        {
            if (Walking)
                return;
            Target = target;
            Walking = true;
        }

        public void Update(float dt)
        {
            if (CurrentAnimation != null)
                CurrentAnimation.Update(dt);
            
            if (!Walking)
                return;

            var targetF = Target.ToCoordF() + new CoordF(0.5f, 0.5f);

            var d = targetF - Position;
            var len = d.Length;

            if (len < 0.01) {
                Position = targetF;
                MapCoord = Position.ToCoord();
                Walking = false;
                return;
            }

            const float speed = 3;
            float moveAmount = speed * dt;
            if (moveAmount > len)
                moveAmount = len;
            d.Normalize();
            Position = Position + (d * moveAmount);
            MapCoord = Position.ToCoord();
        }
    }
}
