using System.Text;
using System.Text.RegularExpressions;
using K4os.Compression.LZ4;

namespace ParaTool.Core.Parsing;

/// <summary>
/// Minimal LSF scanner for BG3 RootTemplate files.
/// Extracts Stats→DisplayName handle mappings without full format parsing.
/// Works across LSOF v5/v6/v7 by scanning raw bytes for known patterns.
/// </summary>
public static partial class LsfScanner
{
    /// <summary>
    /// Scans an LSF file for GameObjects entries and extracts Stats → DisplayName handle mappings.
    /// </summary>
    public static Dictionary<string, string> ScanForDisplayNames(byte[] data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Find all TranslatedString handles in the file
        // Pattern: h + 7-8 hex + g + 4 hex + g + 4 hex + g + 4 hex + g + 12 hex
        var handles = new List<(int offset, string handle)>();
        foreach (Match m in HandleRegex().Matches(Encoding.Latin1.GetString(data)))
        {
            handles.Add((m.Index, m.Value));
        }

        // Find all length-prefixed strings that could be Stats values
        // Stats values are FixedStrings stored as int32 length + UTF-8 data
        var strings = FindPrefixedStrings(data);

        // For single-template files (one GameObjects per .lsf):
        // First handle is typically DisplayName, second is Description
        if (handles.Count >= 1 && strings.Count >= 1)
        {
            // Each string could be a Stats value. The DisplayName handle is the closest
            // preceding TranslatedString handle.
            // In practice for individual RootTemplate files: one Stats + one DisplayName
            foreach (var (strOff, strVal) in strings)
            {
                // Find the nearest handle BEFORE this string offset (DisplayName comes before Stats in LSX order)
                string? nearestHandle = null;
                int nearestDist = int.MaxValue;
                foreach (var (hOff, hVal) in handles)
                {
                    int dist = Math.Abs(hOff - strOff);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestHandle = hVal;
                    }
                }

                if (nearestHandle != null)
                    result[strVal] = nearestHandle;
            }
        }

        return result;
    }

    /// <summary>
    /// Scans an LSF file for ALL Stats attribute values (returns a set of StatIds found).
    /// Useful for quick membership check without full parsing.
    /// </summary>
    public static HashSet<string> ScanForStatIds(byte[] data, IReadOnlySet<string> knownStatIds)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = Encoding.Latin1.GetString(data);

        foreach (var statId in knownStatIds)
        {
            if (text.Contains(statId, StringComparison.Ordinal))
                found.Add(statId);
        }

        return found;
    }

    /// <summary>
    /// Scans raw bytes for GameObjects templates, extracting (Stats, DisplayNameHandle) pairs.
    /// Optimized for batch scanning: only looks for StatIds in the provided set.
    /// </summary>
    public static Dictionary<string, string> ScanForKnownStats(byte[] data, IReadOnlySet<string> statIds)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // First pass: find which statIds exist in this file
        var text = Encoding.Latin1.GetString(data);
        var presentStats = new List<(int offset, string statId)>();

        foreach (var statId in statIds)
        {
            int idx = text.IndexOf(statId, StringComparison.Ordinal);
            if (idx >= 0)
                presentStats.Add((idx, statId));
        }

        if (presentStats.Count == 0) return result;

        // Second pass: find all TranslatedString handles
        var handles = new List<(int offset, string handle)>();
        foreach (Match m in HandleRegex().Matches(text))
        {
            handles.Add((m.Index, m.Value));
        }

        if (handles.Count == 0) return result;

        // For single-template files: first handle = DisplayName
        if (presentStats.Count == 1 && handles.Count >= 1)
        {
            result[presentStats[0].statId] = handles[0].handle;
            return result;
        }

        // For multi-template files: match by proximity
        foreach (var (statOff, statId) in presentStats)
        {
            string? bestHandle = null;
            int bestDist = int.MaxValue;
            foreach (var (hOff, hVal) in handles)
            {
                int dist = Math.Abs(hOff - statOff);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestHandle = hVal;
                }
            }
            if (bestHandle != null)
                result[statId] = bestHandle;
        }

        return result;
    }

    /// <summary>
    /// Finds the nearest TranslatedString handle to a given UUID in the binary data.
    /// Used to find DisplayName for a GameObjects template identified by its MapKey UUID.
    /// </summary>
    /// <summary>
    /// Extract ParentTemplateId value for the given UUID inside the LSF data.
    /// Returns null if not present (template doesn't inherit from another template).
    /// </summary>
    public static string? FindParentTemplateId(byte[] data, string uuid)
    {
        if (!IsLsf(data)) return null;
        var decompressed = TryDecompressLsf(data);
        if (decompressed == null) return null;

        var text = Encoding.Latin1.GetString(decompressed);
        int uuidPos = text.IndexOf(uuid, StringComparison.OrdinalIgnoreCase);
        if (uuidPos < 0) return null;

        // Find node boundaries — next UUID after ours marks the end of this node.
        var guidRx = GuidRegex();
        int nextUuidPos = text.Length;
        foreach (Match m in guidRx.Matches(text))
        {
            if (m.Index > uuidPos + 36)
            {
                nextUuidPos = m.Index;
                break;
            }
        }

        // Look for "ParentTemplateId=<uuid>" within our node
        var nodeText = text[uuidPos..nextUuidPos];
        var ptIdx = nodeText.IndexOf("ParentTemplateId=", StringComparison.OrdinalIgnoreCase);
        if (ptIdx < 0) return null;

        var after = nodeText[(ptIdx + "ParentTemplateId=".Length)..];
        var match = guidRx.Match(after);
        return match.Success ? match.Value : null;
    }

    public static string? FindHandleNearUuid(byte[] data, string uuid)
    {
        var text = Encoding.Latin1.GetString(data);

        // Find UUID position — try as string first, then as binary GUID
        int uuidPos = text.IndexOf(uuid, StringComparison.OrdinalIgnoreCase);
        if (uuidPos < 0)
        {
            if (Guid.TryParse(uuid, out var guid))
            {
                var guidBytes = guid.ToByteArray();
                uuidPos = FindBytes(data, guidBytes);
            }
        }

        // If this is a compressed LSF, decompress sections and search in raw bytes
        if (IsLsf(data))
        {
            var decompressed = TryDecompressLsf(data);
            if (decompressed != null && decompressed.Length > 0)
            {
                // Search for UUID as text in decompressed binary
                var uuidBytes = Encoding.UTF8.GetBytes(uuid);
                int decUuidPos = FindBytes(decompressed, uuidBytes);

                // Also try binary GUID
                if (decUuidPos < 0 && Guid.TryParse(uuid, out var guid2))
                    decUuidPos = FindBytes(decompressed, guid2.ToByteArray());

                if (decUuidPos >= 0)
                {
                    // Search for handle pattern as raw ASCII bytes in decompressed data
                    var decText = Encoding.Latin1.GetString(decompressed);
                    return FindHandleNearUuidInText(decText, decUuidPos);
                }
            }
        }

        if (uuidPos < 0) return null;

        // Find all handles in raw data
        var handles = new List<(int offset, string handle)>();
        foreach (Match m in HandleRegex().Matches(text))
            handles.Add((m.Index, m.Value));

        if (handles.Count == 0) return null;

        // For individual template files (1 GameObjects): first handle = DisplayName
        if (handles.Count <= 2)
            return handles[0].handle;

        // For merged files: find the handle closest AFTER the UUID (DisplayName follows MapKey)
        // but within a reasonable distance (same GameObjects node, ~2KB window)
        string? best = null;
        int bestDist = int.MaxValue;

        foreach (var (hOff, hVal) in handles)
        {
            int dist = hOff - uuidPos;
            // Prefer handles AFTER the UUID (DisplayName comes after MapKey in GameObjects)
            // but also consider handles before (within 500 bytes) in case ordering varies
            if (dist >= 0 && dist < bestDist && dist < 2000)
            {
                bestDist = dist;
                best = hVal;
            }
            else if (dist < 0 && dist > -500 && bestDist == int.MaxValue)
            {
                bestDist = -dist + 10000; // Lower priority
                best = hVal;
            }
        }

        return best;
    }

    private static List<(int offset, string value)> FindPrefixedStrings(byte[] data)
    {
        var result = new List<(int, string)>();

        // Scan for potential FixedString values: int32 length + ASCII string
        for (int i = 0; i < data.Length - 8; i++)
        {
            int len = data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24);
            if (len < 3 || len > 200 || i + 4 + len > data.Length) continue;

            // Check if all bytes are valid identifier characters (Stats IDs are alphanumeric + underscore)
            bool valid = true;
            for (int j = 0; j < len; j++)
            {
                byte b = data[i + 4 + j];
                if (!((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') ||
                      (b >= '0' && b <= '9') || b == '_'))
                {
                    valid = false;
                    break;
                }
            }

            if (valid)
            {
                var str = Encoding.UTF8.GetString(data, i + 4, len);
                // Stats IDs typically start with a letter and contain underscore
                if (str.Contains('_') && char.IsLetter(str[0]))
                    result.Add((i + 4, str));
                i += 3 + len; // skip ahead
            }
        }

        return result;
    }

    /// <summary>
    /// Batch version: decompress once, find handles for all UUIDs.
    /// </summary>
    /// <summary>
    /// Batch version: decompress once, find DisplayName + Description handles for all UUIDs.
    /// Returns (displayNameHandles, descriptionHandles).
    /// </summary>
    public static (Dictionary<string, string> names, Dictionary<string, string> descriptions) FindHandlesForUuidsEx(byte[] data, IReadOnlySet<string> uuids)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var descriptionResult = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Get raw bytes — decompress if LSF
        byte[] raw;
        if (IsLsf(data))
        {
            var decompressed = TryDecompressLsf(data);
            if (decompressed == null) return (result, descriptionResult);
            raw = decompressed;
        }
        else
        {
            raw = data;
        }

        // Find all handles as ASCII text in the raw bytes
        var text = Encoding.Latin1.GetString(raw);
        var handles = new List<(int offset, string handle)>();
        foreach (Match m in HandleRegex().Matches(text))
            handles.Add((m.Index, m.Value));

        if (handles.Count == 0) return (result, descriptionResult);

        // For each UUID: find as text string OR as binary .NET Guid, then find nearest handle
        foreach (var uuid in uuids)
        {
            // Try text match first
            int uuidPos = text.IndexOf(uuid, StringComparison.OrdinalIgnoreCase);

            // Try binary GUID match
            if (uuidPos < 0 && Guid.TryParse(uuid, out var guid))
            {
                var guidBytes = guid.ToByteArray();
                uuidPos = FindBytes(raw, guidBytes);
            }

            if (uuidPos < 0) continue;

            // Find node boundaries: previous UUID and next UUID
            int nodeEnd = FindNextUuidBoundary(raw, text, uuidPos + 36);
            int nodeStart = FindPrevUuidBoundary(text, uuidPos);

            // Find first TWO handles within node boundaries
            // 1st = DisplayName, 2nd = Description
            string? first = null, second = null;
            int firstOff = int.MaxValue, secondOff = int.MaxValue;
            foreach (var (hOff, hVal) in handles)
            {
                if (hOff < nodeStart || hOff >= nodeEnd) continue;
                if (hOff < firstOff)
                {
                    second = first; secondOff = firstOff;
                    first = hVal; firstOff = hOff;
                }
                else if (hOff < secondOff)
                {
                    second = hVal; secondOff = hOff;
                }
            }

            if (first != null)
                result[uuid] = first;
            if (second != null)
                descriptionResult[uuid] = second;
        }

        return (result, descriptionResult);
    }

    /// <summary>
    /// Legacy wrapper — returns only DisplayName handles.
    /// </summary>
    public static Dictionary<string, string> FindHandlesForUuids(byte[] data, IReadOnlySet<string> uuids)
    {
        return FindHandlesForUuidsEx(data, uuids).names;
    }

    private static int FindNextUuidBoundary(byte[] raw, string text, int startPos)
    {
        var textMatch = GuidRegex().Match(text, Math.Min(startPos, text.Length));
        return textMatch.Success ? textMatch.Index : int.MaxValue;
    }

    private static int FindPrevUuidBoundary(string text, int beforePos)
    {
        // Find the last UUID that ends before our position
        int prev = 0;
        foreach (Match m in GuidRegex().Matches(text))
        {
            int end = m.Index + m.Length;
            if (end <= beforePos - 1)
                prev = end;
            else
                break;
        }
        return prev;
    }

    private static string? FindHandleNearUuidInText(string text, int uuidPos)
    {
        var handles = new List<(int offset, string handle)>();
        foreach (Match m in HandleRegex().Matches(text))
            handles.Add((m.Index, m.Value));

        if (handles.Count == 0) return null;
        if (handles.Count <= 2) return handles[0].handle;

        // Collect ALL UUID positions to detect node boundaries
        var allUuids = new List<int>();
        foreach (Match m in GuidRegex().Matches(text))
            allUuids.Add(m.Index);

        // Find the NEXT UUID after our target — that marks the end of our node's data
        int nodeEndBound = int.MaxValue;
        foreach (var uid in allUuids)
        {
            if (uid > uuidPos + 36) // skip ourselves (36 = UUID length)
            {
                nodeEndBound = uid;
                break;
            }
        }

        // Find the PREVIOUS UUID — that marks the start boundary
        int nodeStartBound = 0;
        for (int i = allUuids.Count - 1; i >= 0; i--)
        {
            if (allUuids[i] < uuidPos - 36)
            {
                nodeStartBound = allUuids[i] + 36;
                break;
            }
        }

        // Find handle within node boundaries (between prev UUID and next UUID)
        string? best = null;
        int bestDist = int.MaxValue;
        foreach (var (hOff, hVal) in handles)
        {
            if (hOff < nodeStartBound || hOff > nodeEndBound) continue;
            int dist = Math.Abs(hOff - uuidPos);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = hVal;
            }
        }
        return best;
    }

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.Compiled)]
    private static partial Regex GuidRegex();

    public static bool IsLsf(byte[] data)
    {
        return data.Length >= 4 && data[0] == 'L' && data[1] == 'S' && data[2] == 'O' && data[3] == 'F';
    }

    /// <summary>
    /// Decompresses an LSF file into a text-searchable byte buffer by delegating to
    /// the full LSLib LSFReader. Returns a concatenation of the strings pool + all
    /// node names + all attribute text values + attribute binary data — enough for
    /// UUID and loca-handle text searches to find what's there.
    ///
    /// The previous hand-rolled header parser broke on newer LSF versions (7/8) and
    /// returned zero-filled buffers silently, which is why scanner missed every
    /// handle stored in legacy RootTemplates/_merged.lsf in AMP-style mods.
    /// </summary>
    public static byte[]? TryDecompressLsf(byte[] data)
    {
        try
        {
            if (!IsLsf(data) || data.Length < 16) return null;

            using var ms = new MemoryStream(data, writable: false);
            using var lsfReader = new LSLib.LSFReader(ms);
            var resource = lsfReader.Read();

            // Serialise everything the scanner cares about: region/node names and
            // every attribute value (strings rendered as-is, binary bytes appended).
            using var outMs = new MemoryStream();
            foreach (var region in resource.Regions.Values)
                SerializeNode(region, outMs);
            return outMs.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static void SerializeNode(LSLib.Node node, MemoryStream outMs)
    {
        // Write node name
        if (!string.IsNullOrEmpty(node.Name))
        {
            var nameBytes = System.Text.Encoding.Latin1.GetBytes(node.Name + "\n");
            outMs.Write(nameBytes, 0, nameBytes.Length);
        }

        // Write every attribute's string value — covers UUIDs (stored as strings),
        // handle references, tooltip text, paths, and any other string content.
        foreach (var (attrName, attrValue) in node.Attributes)
        {
            if (!string.IsNullOrEmpty(attrName))
            {
                var k = System.Text.Encoding.Latin1.GetBytes(attrName + "=");
                outMs.Write(k, 0, k.Length);
            }
            var v = attrValue?.Value?.ToString();
            if (!string.IsNullOrEmpty(v))
            {
                var vb = System.Text.Encoding.Latin1.GetBytes(v + "\n");
                outMs.Write(vb, 0, vb.Length);
            }
            // TranslatedString.ToString emits "Handle;Version" when Value is empty
            // (the usual case for DisplayName / Description attrs on mod templates),
            // so we don't duplicate-emit the handle — that would collapse DisplayName
            // and Description into two matches of the same handle when the scanner
            // picks "first 2 handles near UUID".
            if (attrValue?.Value is LSLib.TranslatedString ts
                && !string.IsNullOrEmpty(ts.Handle)
                && !string.IsNullOrEmpty(ts.Value))
            {
                var hb = System.Text.Encoding.Latin1.GetBytes(ts.Handle + "\n");
                outMs.Write(hb, 0, hb.Length);
            }
        }

        foreach (var child in node.Children.Values)
            foreach (var c in child)
                SerializeNode(c, outMs);
    }

    private static void DecompressLz4Block(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        // Check for LZ4 Frame magic (0x184D2204)
        if (src.Length >= 4 && src[0] == 0x04 && src[1] == 0x22 && src[2] == 0x4D && src[3] == 0x18)
        {
            // LZ4 Frame format — use LZ4Stream
            using var input = new MemoryStream(src.ToArray());
            using var decoder = K4os.Compression.LZ4.Streams.LZ4Stream.Decode(input);
            int totalRead = 0;
            while (totalRead < dst.Length)
            {
                int read = decoder.Read(dst[totalRead..]);
                if (read == 0) break;
                totalRead += read;
            }
        }
        else
        {
            // LZ4 Block format
            LZ4Codec.Decode(src, dst);
        }
    }

    private static int FindBytes(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Batch find Icon names near UUIDs in decompressed LSF/LSX data.
    /// Looks for FixedString values that match icon name patterns near each UUID.
    /// </summary>
    public static Dictionary<string, string> FindIconNamesForUuids(byte[] data, IReadOnlySet<string> uuids)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        byte[] raw;
        if (IsLsf(data))
        {
            var decompressed = TryDecompressLsf(data);
            if (decompressed == null) return result;
            raw = decompressed;
        }
        else
        {
            raw = data;
        }

        var text = Encoding.Latin1.GetString(raw);

        // Also try as UTF8 for LSX format
        string? utf8Text = null;
        try { utf8Text = Encoding.UTF8.GetString(raw); } catch { }

        foreach (var uuid in uuids)
        {
            // Find UUID position
            int uuidPos = text.IndexOf(uuid, StringComparison.OrdinalIgnoreCase);
            if (uuidPos < 0 && Guid.TryParse(uuid, out var guid))
                uuidPos = FindBytes(raw, guid.ToByteArray());
            if (uuidPos < 0) continue;

            // Search for "Icon" attribute in LSX format nearby
            if (utf8Text != null)
            {
                // In LSX: <attribute id="Icon" type="FixedString" value="ICON_NAME" />
                var searchRegion = utf8Text.Substring(
                    Math.Max(0, uuidPos - 500),
                    Math.Min(5000, utf8Text.Length - Math.Max(0, uuidPos - 500)));
                var iconMatch = IconLsxRegex().Match(searchRegion);
                if (iconMatch.Success)
                {
                    result[uuid] = iconMatch.Groups[1].Value;
                    continue;
                }
            }

            // In binary LSF: look for known icon name patterns as FixedString
            // Icon names typically contain "Item_", "Ring", "Amulet", "Helmet", etc.
            int searchStart = Math.Max(0, uuidPos - 200);
            int searchEnd = Math.Min(raw.Length, uuidPos + 3000);
            var region = Encoding.Latin1.GetString(raw, searchStart, searchEnd - searchStart);
            var binaryMatch = IconBinaryRegex().Match(region);
            if (binaryMatch.Success)
                result[uuid] = binaryMatch.Value;
        }

        return result;
    }

    [GeneratedRegex(@"h[0-9a-f]{7,8}g[0-9a-f]{4}g[0-9a-f]{4}g[0-9a-f]{4}g[0-9a-f]{10,12}", RegexOptions.Compiled)]
    private static partial Regex HandleRegex();

    [GeneratedRegex(@"id=""Icon""[^>]*value=""([^""]+)""", RegexOptions.Compiled)]
    private static partial Regex IconLsxRegex();

    // Matches common BG3 icon name patterns in binary data
    [GeneratedRegex(@"(?:Item_|Generated_|GEN_)[A-Z][A-Za-z0-9_]{3,60}", RegexOptions.Compiled)]
    private static partial Regex IconBinaryRegex();
}
