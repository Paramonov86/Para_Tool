using ParaTool.Core.LSLib;

namespace ParaTool.Core.Services;

/// <summary>
/// Extracts Icon FixedString from RootTemplate LSF files using full LSF parsing.
/// This is more reliable than binary pattern matching for Icon field extraction.
/// </summary>
public static class RootTemplateIconExtractor
{
    /// <summary>
    /// Parse an LSF file and extract MapKey UUID → Icon name mappings.
    /// </summary>
    public static Dictionary<string, string> ExtractFromLsf(byte[] data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var ms = new MemoryStream(data);
            var reader = new LSFReader(ms);
            var resource = reader.Read();

            foreach (var region in resource.Regions.Values)
            {
                ExtractFromNode(region, result);
            }
        }
        catch
        {
            // LSF parsing failed — fall back to empty
        }

        return result;
    }

    private static void ExtractFromNode(Node node, Dictionary<string, string> result)
    {
        // Check if this node has both MapKey and Icon
        string? mapKey = null;
        string? icon = null;

        foreach (var attr in node.Attributes)
        {
            if (attr.Key.Equals("MapKey", StringComparison.OrdinalIgnoreCase))
                mapKey = attr.Value.Value?.ToString();
            else if (attr.Key.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                icon = attr.Value.Value?.ToString();
        }

        if (mapKey != null && icon != null && !string.IsNullOrEmpty(icon))
            result.TryAdd(mapKey, icon);

        // Recurse into children
        foreach (var childList in node.Children)
        {
            foreach (var child in childList.Value)
                ExtractFromNode(child, result);
        }
    }
}
