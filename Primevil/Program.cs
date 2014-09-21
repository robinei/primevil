using System;

namespace Primevil
{
#if WINDOWS || LINUX
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new XnaGame())
                game.Run();
        }
    }
#endif
}
