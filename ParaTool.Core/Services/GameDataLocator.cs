using System.Text.RegularExpressions;

namespace ParaTool.Core.Services;

/// <summary>
/// Finds the game's <c>Data</c> folder (the one holding Shared.pak, Game.pak, Icons.pak). ParaTool
/// reads the game's own icon atlases and tooltip icons from there. Order: the folder the user set,
/// <c>PARATOOL_GAME_DATA</c>, Steam libraries, GOG, then the usual install paths on every drive.
/// </summary>
public static class GameDataLocator
{
    private const string SteamFolder = "Baldurs Gate 3";
    private const string SteamAppId = "1086940";
    private const string GogGameId = "1456460669";

    /// <summary>A Data folder: it holds the paks ParaTool reads icons from.</summary>
    public static bool IsDataDir(string? dir) =>
        !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "Shared.pak")) && File.Exists(Path.Combine(dir, "Game.pak"));

    /// <summary>
    /// Accepts the game folder, its Data folder or its bin folder and returns the Data folder, or
    /// null when none of them is an install.
    /// </summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim().Trim('"');
        foreach (var candidate in new[] { path, Path.Combine(path, "Data"), Path.Combine(path, "..", "Data") })
        {
            try
            {
                var full = Path.GetFullPath(candidate);
                if (IsDataDir(full)) return full;
            }
            catch { /* malformed path */ }
        }
        return null;
    }

    public static string? Find(string? userPath = null)
    {
        if (Normalize(userPath) is { } user) return user;
        if (Normalize(Environment.GetEnvironmentVariable("PARATOOL_GAME_DATA")) is { } env) return env;

        foreach (var root in SteamRoots().Concat(GogRoots()).Concat(CommonRoots()))
            if (Normalize(root) is { } found) return found;
        return null;
    }

    private static IEnumerable<string> SteamRoots()
    {
        var steamDirs = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            steamDirs.AddRange(new[]
            {
                ReadRegistry(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath"),
                ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                ReadRegistry(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath"),
            }.OfType<string>());
            var uninstall = ReadRegistry($@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {SteamAppId}", "InstallLocation");
            if (uninstall != null) yield return uninstall;
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            steamDirs.Add(Path.Combine(home, ".steam", "steam"));
            steamDirs.Add(Path.Combine(home, ".local", "share", "Steam"));
            steamDirs.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
        }

        foreach (var steam in steamDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.Combine(steam, "steamapps", "common", SteamFolder);
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            string text;
            try { text = File.Exists(vdf) ? File.ReadAllText(vdf) : ""; }
            catch { continue; }
            foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
                yield return Path.Combine(m.Groups[1].Value.Replace(@"\\", @"\"), "steamapps", "common", SteamFolder);
        }
    }

    private static IEnumerable<string> GogRoots()
    {
        if (!OperatingSystem.IsWindows()) yield break;
        foreach (var key in new[] { $@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games\{GogGameId}", $@"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\Games\{GogGameId}" })
            if (ReadRegistry(key, "path") is { } path) yield return path;
    }

    private static IEnumerable<string> CommonRoots()
    {
        if (!OperatingSystem.IsWindows()) yield break;
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch { yield break; }
        foreach (var drive in drives)
        {
            if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
            var root = drive.RootDirectory.FullName;
            yield return Path.Combine(root, "SteamLibrary", "steamapps", "common", SteamFolder);
            yield return Path.Combine(root, "Steam", "steamapps", "common", SteamFolder);
            yield return Path.Combine(root, "Program Files (x86)", "Steam", "steamapps", "common", SteamFolder);
            yield return Path.Combine(root, "Program Files", "Steam", "steamapps", "common", SteamFolder);
            yield return Path.Combine(root, "Games", "Steam", "steamapps", "common", SteamFolder);
            yield return Path.Combine(root, "GOG Games", "Baldur's Gate 3");
            yield return Path.Combine(root, "Games", "Baldur's Gate 3");
        }
    }

    private static string? ReadRegistry(string key, string value)
    {
        if (!OperatingSystem.IsWindows()) return null;
        try { return Microsoft.Win32.Registry.GetValue(key, value, null) as string; }
        catch { return null; }
    }
}
