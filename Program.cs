static class Program
{
    [STAThread]
    static void Main()
    {
        if (OperatingSystem.IsLinux()
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ALSOFT_DRIVERS")))
        {
            Environment.SetEnvironmentVariable("ALSOFT_DRIVERS", "pulse,alsa");
        }

        // Must be set before SDL initializes (MonoGame creates the window on first Game construction).
        Environment.SetEnvironmentVariable("SDL_JOYSTICK_HIDAPI", "1");

        using var game = new VectorBreakout.Game1();
        game.Run();
    }
}
