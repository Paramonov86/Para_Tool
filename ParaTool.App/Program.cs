using Avalonia;

namespace ParaTool.App;

class Program
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ParaTool", "crash.log");

    [STAThread]
    public static int Main(string[] args)
    {
        // Global exception handlers — log to file
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash("UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        // Headless diagnostic mode — used by devs to dump item resolution state.
        // Skips the Avalonia UI entirely so the process can run and exit in a
        // few seconds (scan + dump).
        if (args.Any(a => a.StartsWith("--diag", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                return DiagMode.RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                LogCrash("DiagMode", ex);
                Console.Error.WriteLine($"DIAG FAILED: {ex}");
                return 1;
            }
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            LogCrash("Main", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void LogCrash(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}]\n{ex}\n\n";
            File.AppendAllText(LogPath, msg);
        }
        catch { /* can't log, give up */ }
    }
}
