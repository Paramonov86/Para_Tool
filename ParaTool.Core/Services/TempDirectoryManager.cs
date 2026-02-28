namespace ParaTool.Core.Services;

public sealed class TempDirectoryManager : IDisposable
{
    private readonly string _basePath;
    private bool _disposed;

    public TempDirectoryManager()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "ParaTool_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_basePath);
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
