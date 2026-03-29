using ParaTool.Core;
using Xunit;

namespace ParaTool.Tests;

public class PakVerifyTest
{
    [Fact]
    public void VerifyArtifactInPak()
    {
        var pakPath = @"C:\Users\user\AppData\Local\Larian Studios\Baldur's Gate 3\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025.pak";
        if (!File.Exists(pakPath)) return;

        using var fs = File.OpenRead(pakPath);
        var header = PakReader.ReadHeader(fs);
        var entries = PakReader.ReadFileList(fs, header);

        var rtEntries = entries.Where(e => e.Path.Contains("823565e7", StringComparison.OrdinalIgnoreCase)).ToList();
        var paraEntries = entries.Where(e => e.Path.Contains("ParaTool", StringComparison.OrdinalIgnoreCase)).ToList();

        var output = $"Total files: {entries.Count}\n";
        output += $"RootTemplate 823565e7: {rtEntries.Count}\n";
        foreach (var e in rtEntries) output += $"  {e.Path}\n";
        output += $"ParaTool entries: {paraEntries.Count}\n";
        foreach (var e in paraEntries) output += $"  {e.Path}\n";

        // Find stat files and check last one for AMP_Test05
        var statEntries = entries.Where(e => e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            && e.Path.Contains("Stats", StringComparison.OrdinalIgnoreCase)
            && e.Path.Contains("Generated", StringComparison.OrdinalIgnoreCase)).ToList();
        output += $"Stat files: {statEntries.Count}\n";
        foreach (var se in statEntries) output += $"  {se.Path}\n";

        // Check each stat file for AMP_Test_06
        foreach (var se in statEntries)
        {
            var data = PakReader.ExtractFileData(fs, se);
            var text = System.Text.Encoding.UTF8.GetString(data);
            if (text.Contains("AMP_Test_06"))
            {
                output += $"\nFOUND AMP_Test_06 in {se.Path}\n";
                var idx = text.IndexOf("AMP_Test_06");
                // grab the full entry block
                var blockStart = text.LastIndexOf("new entry", idx, StringComparison.OrdinalIgnoreCase);
                if (blockStart < 0) blockStart = Math.Max(0, idx - 20);
                var blockEnd = text.IndexOf("\nnew entry", idx, StringComparison.OrdinalIgnoreCase);
                if (blockEnd < 0) blockEnd = Math.Min(text.Length, idx + 800);
                output += text[blockStart..blockEnd] + "\n";
            }
        }

        // Check TreasureTable
        var ttIdx = entries.FindIndex(e => e.Path.Contains("TreasureTable", StringComparison.OrdinalIgnoreCase));
        if (ttIdx >= 0)
        {
            var ttData = PakReader.ExtractFileData(fs, entries[ttIdx]);
            var ttText = System.Text.Encoding.UTF8.GetString(ttData);
            output += $"\nTreasureTable contains AMP_Test_06: {ttText.Contains("AMP_Test_06")}\n";
        }

        // Check RootTemplate LSF
        var rtEntries2 = entries.Where(e => e.Path.Contains("76664a3f", StringComparison.OrdinalIgnoreCase)).ToList();
        output += $"\nRootTemplate 76664a3f: {rtEntries2.Count}\n";
        foreach (var e in rtEntries2) output += $"  {e.Path}\n";

        // Check for any .lsf in RootTemplates that might be ours
        var allRt = entries.Where(e => e.Path.Contains("RootTemplates") && e.Path.EndsWith(".lsf")).ToList();
        output += $"\nAll RootTemplate .lsf files: {allRt.Count}\n";
        // Show last 5
        foreach (var e in allRt.TakeLast(5)) output += $"  {e.Path}\n";

        // Dump last stat file fully to check syntax
        var lastStatEntry = statEntries.OrderBy(e => e.Path).LastOrDefault();
        if (lastStatEntry.Path != null)
        {
            var ldata = PakReader.ExtractFileData(fs, lastStatEntry);
            var ltext = System.Text.Encoding.UTF8.GetString(ldata);
            // Last 500 chars
            output += $"\n--- Last 500 chars of {lastStatEntry.Path} ---\n";
            output += ltext[Math.Max(0, ltext.Length - 500)..] + "\n";
        }

        // Check metadata.lsx
        var metaIdx = entries.FindIndex(e => e.Path.Contains("metadata.lsx", StringComparison.OrdinalIgnoreCase));
        if (metaIdx >= 0)
        {
            output += $"\nmetadata.lsx: FOUND at {entries[metaIdx].Path}\n";
        }
        var metaLsfIdx = entries.FindIndex(e => e.Path.EndsWith("metadata.lsf", StringComparison.OrdinalIgnoreCase));
        output += $"metadata.lsf: {(metaLsfIdx >= 0 ? "FOUND" : "DELETED (good)")}\n";

        // Check loca files for handles
        var locaEntries = entries.Where(e => e.Path.Contains("Localization") &&
            (e.Path.EndsWith(".xml") || e.Path.EndsWith(".loca.xml"))).ToList();
        output += $"\nLoca files: {locaEntries.Count}\n";
        foreach (var le in locaEntries) output += $"  {le.Path}\n";

        // Search for our handle in loca files
        var handle = "h808157fa";
        foreach (var le in locaEntries)
        {
            var ld = PakReader.ExtractFileData(fs, le);
            var lt = System.Text.Encoding.UTF8.GetString(ld);
            if (lt.Contains(handle))
            {
                output += $"\nHandle {handle} FOUND in {le.Path}\n";
                var hi = lt.IndexOf(handle);
                output += lt.Substring(Math.Max(0, hi - 20), Math.Min(200, lt.Length - Math.Max(0, hi - 20))) + "\n";
            }
        }

        File.WriteAllText(@"C:\Users\user\AppData\Local\Temp\pak_verify.txt", output);
    }
}
