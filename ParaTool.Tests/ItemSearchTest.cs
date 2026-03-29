using ParaTool.Core;
using Xunit;

namespace ParaTool.Tests;

public class ItemSearchTest
{
    [Fact]
    public void SearchItems()
    {
        var pakPath = @"C:\Users\user\AppData\Local\Larian Studios\Baldur's Gate 3\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025.pak";
        if (!File.Exists(pakPath)) return;

        using var fs = File.OpenRead(pakPath);
        var header = PakReader.ReadHeader(fs);
        var entries = PakReader.ReadFileList(fs, header);

        var targets = new[] { "MAG_Ring35_1", "AMP_RusMage_Dagger_1", "AMP_Test_06" };
        var output = "";

        var statEntries = entries.Where(e => e.Path.EndsWith(".txt") &&
            e.Path.Contains("Stats") && e.Path.Contains("Generated") && e.Path.Contains("Data")).ToList();

        foreach (var se in statEntries)
        {
            var data = PakReader.ExtractFileData(fs, se);
            var text = System.Text.Encoding.UTF8.GetString(data);

            foreach (var target in targets)
            {
                var idx = text.IndexOf($"new entry \"{target}\"", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;

                // Find end of entry
                var nextEntry = text.IndexOf("\nnew entry ", idx + 10, StringComparison.OrdinalIgnoreCase);
                var end = nextEntry > 0 ? nextEntry : Math.Min(text.Length, idx + 500);
                var entry = text[idx..end].Trim();

                output += $"=== {target} in {Path.GetFileName(se.Path)} ===\n{entry}\n\n";
            }
        }

        if (string.IsNullOrEmpty(output)) output = "None of the target items found in stat files\n";

        File.WriteAllText(@"C:\Users\user\AppData\Local\Temp\item_search.txt", output);
    }
}
