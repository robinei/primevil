using System;

namespace Primevil
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new XnaGame())
                game.Run();
        }
    }
}
