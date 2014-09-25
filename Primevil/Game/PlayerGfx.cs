using System;
using System.Text;

namespace Primevil.Game
{
    public static class PlayerGfx
    {
        public enum Class
        {
            Warrior,
            Rogue,
            Sorceror
        }

        public enum Armor
        {
            Light,
            Medium,
            Heavy
        }

        public enum Weapon
        {
            None,
            Sword,
            Mace,
            SwordShield,
            MaceShield,
            Shield,
            Staff,
            Axe,
            Bow
        }

        public enum State
        {
            StandingInTown,
            WalkingInTown,
            Standing,
            Walking,
            Attacking,
            Blocking,
            Recovery,
            CastFire,
            CastLightning,
            CastMagic,
            Death
        }


        private static readonly int MaxClass = Enum.GetValues(typeof(Class)).Length;
        private static readonly int MaxArmor = Enum.GetValues(typeof(Armor)).Length;
        private static readonly int MaxWeapon = Enum.GetValues(typeof(Weapon)).Length;
        private static readonly int MaxState = Enum.GetValues(typeof(State)).Length;

        private static readonly string[, , ,] Anims = new string[MaxClass, MaxArmor, MaxWeapon, MaxState];


        public static string GetAnimPath(Class cls, Armor armor, Weapon weapon, State state)
        {
            return Anims[(int)cls, (int)armor, (int)weapon, (int)state];
        }


        static PlayerGfx()
        {
            for (int cls = 0; cls < MaxClass; ++cls)
                for (int armor = 0; armor < MaxArmor; ++armor)
                    for (int weapon = 0; weapon < MaxWeapon; ++weapon)
                        for (int state = 0; state < MaxState; ++state)
                            Anims[cls, armor, weapon, state] =
                                MakeAnim((Class)cls, (Armor)armor, (Weapon)weapon, (State)state);
        }

        private static string MakeAnim(Class cls, Armor armor, Weapon weapon, State state)
        {
            var path = new StringBuilder("plrgfx/");
            bool hasBlock = false;

            string classLetter = "w";
            switch (cls) {
            case Class.Warrior:
                path.Append("warrior/");
                classLetter = "w";
                break;
            case Class.Rogue:
                path.Append("rogue/");
                classLetter = "r";
                break;
            case Class.Sorceror:
                path.Append("sorceror/");
                classLetter = "s";
                break;
            }

            string armorLetter = "l";
            switch (armor) {
            case Armor.Light:
                armorLetter = "l";
                break;
            case Armor.Medium:
                armorLetter = "m";
                break;
            case Armor.Heavy:
                armorLetter = "h";
                break;
            }

            string weaponLetter = "n";
            switch (weapon) {
            case Weapon.None:
                weaponLetter = "n";
                break;
            case Weapon.Sword:
                weaponLetter = "s";
                break;
            case Weapon.Mace:
                weaponLetter = "m";
                break;
            case Weapon.SwordShield:
                weaponLetter = "d";
                hasBlock = true;
                break;
            case Weapon.MaceShield:
                weaponLetter = "h";
                hasBlock = true;
                break;
            case Weapon.Shield:
                weaponLetter = "u";
                hasBlock = true;
                break;
            case Weapon.Staff:
                weaponLetter = "t";
                break;
            case Weapon.Axe:
                weaponLetter = "a";
                break;
            case Weapon.Bow:
                weaponLetter = "b";
                break;
            }

            string stateLetter = "as";
            switch (state) {
            case State.StandingInTown:
                stateLetter = "st";
                break;
            case State.WalkingInTown:
                stateLetter = "wl";
                break;
            case State.Standing:
                stateLetter = "as";
                break;
            case State.Walking:
                stateLetter = "aw";
                break;
            case State.Attacking:
                stateLetter = "at";
                break;
            case State.Blocking:
                stateLetter = hasBlock ? "bl" : "as";
                break;
            case State.Recovery:
                stateLetter = "ht";
                break;
            case State.CastFire:
                stateLetter = "fm";
                break;
            case State.CastLightning:
                stateLetter = "lm";
                break;
            case State.CastMagic:
                stateLetter = "qm";
                break;
            case State.Death:
                weaponLetter = "n";
                stateLetter = "dt";
                break;
            }

            path.Append(classLetter);
            path.Append(armorLetter);
            path.Append(weaponLetter);
            path.Append("/");
            path.Append(classLetter);
            path.Append(armorLetter);
            path.Append(weaponLetter);
            path.Append(stateLetter);
            path.Append(".cl2");

            return path.ToString();
        }
    }
}
