using System;
using System.IO;
using System.Text;
using ParaTool.Core;
using ParaTool.Core.Models;

var pakPath = @"C:\Users\user\AppData\Local\Larian Studios\Baldur's Gate 3\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025.pak";
using var fs = File.OpenRead(pakPath);
var header = PakReader.ReadHeader(fs);
var entries = PakReader.ReadFileList(fs, header);
foreach (var e in entries)
{
    if (!e.Path.EndsWith(".txt")) continue;
    var data = PakReader.ExtractFileData(fs, e);
    var text = Encoding.UTF8.GetString(data);
    if (text.Contains("AMP_Void_Pendant"))
    {
        var idx = text.IndexOf("new entry \"AMP_Void_Pendant\"");
        if (idx < 0) continue;
        var end = idx + 3000;
        if (end > text.Length) end = text.Length;
        Console.WriteLine(text[idx..end]);
        break;
    }
}
// Count MAG_Neck16_2_bonus occurrences
fs.Position = 0;
foreach (var e in entries)
{
    if (!e.Path.EndsWith(".txt")) continue;
    var data = PakReader.ExtractFileData(fs, e);
    var text = Encoding.UTF8.GetString(data);
    int count = 0, idx2 = 0;
    while ((idx2 = text.IndexOf("MAG_Neck16_2_bonus", idx2)) >= 0) { count++; idx2++; }
    if (count > 0)
        Console.WriteLine($"\n[COUNT] MAG_Neck16_2_bonus in {e.Path}: {count}");
}
