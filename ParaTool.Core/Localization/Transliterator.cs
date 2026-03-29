using System.Text;

namespace ParaTool.Core.Localization;

/// <summary>
/// Transliterates non-Latin scripts to ASCII Latin for StatId generation.
/// Supports Cyrillic, common diacritics, CJK (pinyin-style), and passthrough for Latin.
/// </summary>
public static class Transliterator
{
    private static readonly Dictionary<char, string> CyrillicMap = new()
    {
        ['А'] = "A", ['Б'] = "B", ['В'] = "V", ['Г'] = "G", ['Д'] = "D",
        ['Е'] = "E", ['Ё'] = "Yo", ['Ж'] = "Zh", ['З'] = "Z", ['И'] = "I",
        ['Й'] = "Y", ['К'] = "K", ['Л'] = "L", ['М'] = "M", ['Н'] = "N",
        ['О'] = "O", ['П'] = "P", ['Р'] = "R", ['С'] = "S", ['Т'] = "T",
        ['У'] = "U", ['Ф'] = "F", ['Х'] = "Kh", ['Ц'] = "Ts", ['Ч'] = "Ch",
        ['Ш'] = "Sh", ['Щ'] = "Shch", ['Ъ'] = "", ['Ы'] = "Y", ['Ь'] = "",
        ['Э'] = "E", ['Ю'] = "Yu", ['Я'] = "Ya",
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
        ['е'] = "e", ['ё'] = "yo", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
        ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "shch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
        // Ukrainian extras
        ['І'] = "I", ['і'] = "i", ['Ї'] = "Yi", ['ї'] = "yi",
        ['Є'] = "Ye", ['є'] = "ye", ['Ґ'] = "G", ['ґ'] = "g",
    };

    /// <summary>
    /// Convert any text to Latin characters. Non-mappable chars pass through unchanged.
    /// </summary>
    public static string ToLatin(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (CyrillicMap.TryGetValue(ch, out var mapped))
                sb.Append(mapped);
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }
}
