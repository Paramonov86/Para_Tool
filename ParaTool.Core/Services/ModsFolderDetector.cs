namespace ParaTool.Core.Services;

public static class ModsFolderDetector
{
    public static string? Detect()
    {
        if (OperatingSystem.IsWindows())
            return DetectWindows();
        if (OperatingSystem.IsLinux())
            return DetectLinux();
        return null;
    }

    private static string? DetectWindows()
    {
        // Standard path: %LOCALAPPDATA%\Larian Studios\Baldur's Gate 3\Mods
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            var path = Path.Combine(localAppData, "Larian Studios", "Baldur's Gate 3", "Mods");
            if (Directory.Exists(path))
                return path;
        }

        // Scan drives for Users\*\AppData\Local\Larian Studios\...
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.Fixed) continue;
            try
            {
                var usersDir = Path.Combine(drive.Name, "Users");
                if (!Directory.Exists(usersDir)) continue;

                foreach (var userDir in Directory.GetDirectories(usersDir))
                {
                    var candidate = Path.Combine(userDir, "AppData", "Local",
                        "Larian Studios", "Baldur's Gate 3", "Mods");
                    if (Directory.Exists(candidate))
                        return candidate;
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return null;
    }

    private static string? DetectLinux()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(home, ".local", "share", "Larian Studios", "Baldur's Gate 3", "Mods");
        return Directory.Exists(path) ? path : null;
    }
}
