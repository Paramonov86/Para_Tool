using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace ParaTool.Core.Services;

public class UpdateService
{
    private const string RepoApiUrl = "https://api.github.com/repos/Paramonov86/Para_Tool/releases/latest";

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "ParaTool-Updater" },
            { "Accept", "application/vnd.github.v3+json" }
        },
        Timeout = TimeSpan.FromSeconds(30)
    };

    public record UpdateInfo(string Version, string DownloadUrl, string Body);

    /// <summary>
    /// Check GitHub for a newer release. Returns null if up-to-date.
    /// </summary>
    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        var release = await Http.GetFromJsonAsync<GitHubRelease>(RepoApiUrl, ct);
        if (release?.TagName == null) return null;

        var remoteVersion = ParseVersion(release.TagName);
        var localVersion = ParseVersion(currentVersion);
        if (remoteVersion == null || localVersion == null || remoteVersion <= localVersion)
            return null;

        var rid = GetRuntimeId();
        var asset = release.Assets?.FirstOrDefault(a =>
            a.Name != null && a.Name.Contains(rid, StringComparison.OrdinalIgnoreCase));
        if (asset?.BrowserDownloadUrl == null) return null;

        return new UpdateInfo(
            release.TagName.TrimStart('v'),
            asset.BrowserDownloadUrl,
            release.Body ?? "");
    }

    /// <summary>
    /// Download the zip and extract to a temp folder. Returns path to the extracted directory.
    /// </summary>
    public async Task<string> DownloadAndExtractAsync(
        string url, IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"ParaTool_Update_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var zipPath = Path.Combine(tempDir, "update.zip");

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        long downloadedBytes = 0;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            downloadedBytes += bytesRead;
            if (totalBytes > 0)
                progress?.Report((int)(downloadedBytes * 100 / totalBytes));
        }

        fileStream.Close();

        var extractDir = Path.Combine(tempDir, "extracted");
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
        File.Delete(zipPath);

        return extractDir;
    }

    /// <summary>
    /// Create an updater script that replaces the app files and relaunches.
    /// </summary>
    public void ApplyAndRestart(string extractedDir, string currentAppDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            ApplyWindows(extractedDir, currentAppDir);
        else
            ApplyLinux(extractedDir, currentAppDir);
    }

    private static void ApplyWindows(string extractedDir, string currentAppDir)
    {
        var currentExe = Environment.ProcessPath!;
        var batPath = Path.Combine(Path.GetTempPath(), "ParaTool_Update.bat");
        var exeName = Path.GetFileName(currentExe);

        // The zip may contain a single subfolder — find where the exe is
        var sourceDir = FindExeDirectory(extractedDir, exeName) ?? extractedDir;

        var script = $"""
            @echo off
            timeout /t 4 /nobreak >nul
            xcopy /s /y /q "{sourceDir}\*" "{currentAppDir}\"
            start "" "{currentExe}"
            timeout /t 2 /nobreak >nul
            rmdir /s /q "{Path.GetDirectoryName(sourceDir)}"
            del "%~f0"
            """;

        File.WriteAllText(batPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        Environment.Exit(0);
    }

    private static void ApplyLinux(string extractedDir, string currentAppDir)
    {
        var currentExe = Environment.ProcessPath!;
        var shPath = Path.Combine(Path.GetTempPath(), "ParaTool_Update.sh");
        var exeName = Path.GetFileName(currentExe);

        var sourceDir = FindExeDirectory(extractedDir, exeName) ?? extractedDir;

        var script = $"""
            #!/bin/bash
            sleep 2
            cp -rf "{sourceDir}/"* "{currentAppDir}/"
            chmod +x "{currentExe}"
            "{currentExe}" &
            rm -rf "{Path.GetDirectoryName(sourceDir)}"
            rm -- "$0"
            """;

        File.WriteAllText(shPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = shPath,
            CreateNoWindow = true,
            UseShellExecute = false
        });

        Environment.Exit(0);
    }

    private static string? FindExeDirectory(string searchRoot, string exeName)
    {
        foreach (var file in Directory.EnumerateFiles(searchRoot, exeName, SearchOption.AllDirectories))
            return Path.GetDirectoryName(file);
        return null;
    }

    private static Version? ParseVersion(string v)
    {
        v = v.TrimStart('v');
        return Version.TryParse(v, out var result) ? result : null;
    }

    private static string GetRuntimeId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.X86 ? "win-x86" : "win-x64";
        return "linux-x64";
    }

    // GitHub API DTOs
    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    }
}
