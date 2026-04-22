using ParaTool.Core;
using ParaTool.Core.Services;

namespace ParaTool.App;

/// <summary>
/// Quick-and-dirty scanner that hunts for a RootTemplate UUID across all paks
/// and extracts any context around it — DisplayName handles, surrounding
/// attributes, whether the pak is shadowing vanilla. Used by --diag-uuid.
/// </summary>
internal static class TemplateFinder
{
    public static List<Dictionary<string, object?>> FindUuid(string uuid, string[] pakPaths)
    {
        var results = new List<Dictionary<string, object?>>();

        foreach (var pakPath in pakPaths)
        {
            try
            {
                using var fs = File.OpenRead(pakPath);
                var header = PakReader.ReadHeader(fs);
                var entries = PakReader.ReadFileList(fs, header);

                // Narrow to .lsf/.lsx template files
                var templateFiles = entries.Where(e =>
                    (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) &&
                    (e.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.Contains("Content", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.Contains("Globals", StringComparison.OrdinalIgnoreCase))).ToList();

                foreach (var entry in templateFiles)
                {
                    byte[] data;
                    try { data = PakReader.ExtractFileData(fs, entry); }
                    catch { continue; }

                    // Latin1 to keep byte positions intact for handle regex
                    var text = System.Text.Encoding.Latin1.GetString(data);
                    var idx = text.IndexOf(uuid, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;

                    // Extract ~4KB window around UUID match for context
                    var windowStart = Math.Max(0, idx - 2048);
                    var windowEnd = Math.Min(text.Length, idx + 2048);
                    var window = text[windowStart..windowEnd];

                    // All loca-like handles in the window
                    var handleRx = new System.Text.RegularExpressions.Regex(
                        @"h[0-9a-f]{7,8}g[0-9a-f]{4}g[0-9a-f]{4}g[0-9a-f]{4}g[0-9a-f]{10,12}",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var handles = handleRx.Matches(window).Select(m => m.Value).Distinct().ToArray();

                    results.Add(new Dictionary<string, object?>
                    {
                        ["pak"] = Path.GetFileName(pakPath),
                        ["templateFile"] = entry.Path,
                        ["uuidOffsetInFile"] = idx,
                        ["fileBytes"] = data.Length,
                        ["handlesNearUuid"] = handles,
                        ["contextSample"] = window.Replace("\0", " ").Trim(),
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new Dictionary<string, object?>
                {
                    ["pak"] = Path.GetFileName(pakPath),
                    ["error"] = ex.Message,
                });
            }
        }

        return results;
    }
}
