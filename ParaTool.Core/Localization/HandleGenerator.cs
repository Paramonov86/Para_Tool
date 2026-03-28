namespace ParaTool.Core.Localization;

/// <summary>
/// Generates BG3-compatible localization handles.
/// Handle format: h{uuid with dashes replaced by g}
/// Example: h5bb2726cg6840g4bc8g82c0g30bf483ee1b7
/// </summary>
public static class HandleGenerator
{
    /// <summary>
    /// Generate a new unique BG3 localization handle.
    /// </summary>
    public static string New()
    {
        return "h" + Guid.NewGuid().ToString().Replace('-', 'g');
    }

    /// <summary>
    /// Generate a handle pair (DisplayName + Description) for a new item.
    /// </summary>
    public static (string displayNameHandle, string descriptionHandle) NewPair()
    {
        return (New(), New());
    }

    /// <summary>
    /// Format a handle with version for use in Stats data fields.
    /// Example: "h5bb2726cg6840g4bc8g82c0g30bf483ee1b7;1"
    /// </summary>
    public static string FormatWithVersion(string handle, int version = 1)
    {
        return $"{handle};{version}";
    }

    /// <summary>
    /// Parse a Stats handle reference like "h...;1" into handle and version.
    /// </summary>
    public static (string handle, int version) Parse(string handleRef)
    {
        var parts = handleRef.Split(';');
        var handle = parts[0];
        var version = parts.Length > 1 && int.TryParse(parts[1], out var v) ? v : 1;
        return (handle, version);
    }

    /// <summary>
    /// Validate that a string looks like a BG3 handle.
    /// </summary>
    public static bool IsValid(string handle)
    {
        return handle.Length >= 33 && handle[0] == 'h' && handle.Contains('g');
    }
}
