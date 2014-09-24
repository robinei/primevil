using Primevil.Formats;

namespace Primevil.Game
{
    public class Level
    {
        public Map Map;
        public TextureAtlas Tileset;
        public MINFile PillarDefs;

        public readonly IntrusiveList<Creature, LevelCreatureList> Creatures = new IntrusiveList<Creature, LevelCreatureList>();


        public Level()
        {
        }
    }
}

