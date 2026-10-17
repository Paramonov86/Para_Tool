using ParaTool.App.Localization;
using ParaTool.Core.Schema;

namespace ParaTool.App.Controls;

public static class BoostLabels
{
    /// <summary>The chip's title in the UI language (the flag is kept for callers; the language decides).</summary>
    public static string GetLabel(BoostMapping.BlockDef def, bool isRu)
        => Loc.Instance.BlockLabel(def);

    public static string GetCategoryLabel(string categoryKey, bool isRu)
        => Loc.Instance[$"BoostCat_{categoryKey}"];
}
