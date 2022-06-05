using System.Drawing;

namespace Yt2ogg
{
    public static class ConsoleAnimator
    {
        private static Task? CurrentAnimator { get; set; } = null;
        public static bool Animating { get; private set; } = false;

        public static void Stop()
        {
            if (CurrentAnimator == null) return;

            Animating = false;
            CurrentAnimator.Wait();
            CurrentAnimator = null;
        }
        public static void Working()
        {
            if (Animating) Stop();
            Animating = true;

            CurrentAnimator = Task.Run(() =>
            {
                int left = Console.GetCursorPosition().Left;
                int state = 0;
                Console.CursorVisible = false;

                while (Animating)
                {
                    Thread.Sleep(100);

                    state = ++state % 4;
                    Console.SetCursorPosition(left, Console.GetCursorPosition().Top);

                    if (state == 0) Console.Write(" /");
                    else if (state == 1) Console.Write(" |");
                    else if (state == 2) Console.Write(" \\");
                    else if (state == 3) Console.Write(" |");
                }

                Console.SetCursorPosition(left, Console.GetCursorPosition().Top);
                Console.WriteLine("  ");
                Console.CursorVisible = true;
            });
        }
    }
}
