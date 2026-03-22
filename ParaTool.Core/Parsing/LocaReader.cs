using System.Text;
using System.Xml;

namespace ParaTool.Core.Parsing;

/// <summary>
/// Reads localization files (.loca.xml, .xml) containing translated strings.
/// Builds a handle → text mapping.
/// </summary>
public static partial class LocaReader
{
    /// <summary>
    /// Parses a loca XML file and returns handle → text mapping.
    /// Supports both .loca.xml and plain .xml formats with &lt;content contentuid="handle"&gt; elements.
    /// </summary>
    public static Dictionary<string, string> ParseXml(byte[] data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var text = Encoding.UTF8.GetString(data);

            // Use regex — XmlReader chokes on embedded HTML/LSTag content in loca files
            foreach (System.Text.RegularExpressions.Match m in
                ContentRegex().Matches(text))
            {
                var uid = m.Groups[1].Value;
                var content = m.Groups[2].Value;
                result[uid] = content;
            }
        }
        catch
        {
            // Skip malformed files silently
        }

        return result;
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"contentuid=""([^""]+)""[^>]*>(.*?)</content>",
        System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Compiled)]
    private static partial System.Text.RegularExpressions.Regex ContentRegex();

    /// <summary>
    /// Checks if the data looks like an XML loca file (starts with &lt;? or BOM+&lt;).
    /// </summary>
    public static bool IsXmlLoca(byte[] data)
    {
        if (data.Length < 4) return false;

        // Check for XML content: <?xml, BOM+<, or just <contentList/<content
        return (data[0] == '<') ||                                                          // <anything
               (data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF && data[3] == '<');  // BOM + <
    }
}
