namespace ParaTool.Core.Services;

public sealed class TempDirectoryManager : IDisposable
{
    private readonly string _basePath;
    private bool _disposed;

    public TempDirectoryManager()
    {
        CleanupStale();
        _basePath = Path.Combine(Path.GetTempPath(), "ParaTool_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>
    /// Deletes leftover temp folders from previous interrupted runs. Only names this class
    /// creates (ParaTool_ + 8 hex digits) — a plain ParaTool_* glob also wiped other folders
    /// under %TEMP% that merely share the prefix, including ones still in use.
    /// </summary>
    private static void CleanupStale()
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), "ParaTool_????????"))
            {
                var suffix = Path.GetFileName(dir)["ParaTool_".Length..];
                if (suffix.Length != 8 || !suffix.All(Uri.IsHexDigit)) continue;
                try { Directory.Delete(dir, recursive: true); }
                catch { /* in use or no access — skip */ }
            }
        }
        catch { /* temp folder inaccessible — ignore */ }
    }

    public string BasePath => _basePath;

    public string CreateSubDirectory(string name)
    {
        var path = Path.Combine(_basePath, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (Directory.Exists(_basePath))
                Directory.Delete(_basePath, recursive: true);
        }
        catch { /* best effort cleanup */ }
    }
}
