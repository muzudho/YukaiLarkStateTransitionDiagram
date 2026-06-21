namespace YukaiLarkStateTransitionDiagram;

internal static class Program
{
    [System.STAThread]
    private static void Main()
    {
        System.Environment.SetEnvironmentVariable("SDL_IME_SHOW_UI", "1");

        using var game = new Game1();
        game.Run();
    }
}