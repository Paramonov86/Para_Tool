using ParaTool.App.Localization;
using ParaTool.Core.Schema;

namespace ParaTool.App.Controls;

public static class BoostLabels
{
    public static string GetLabel(BoostMapping.BlockDef def, bool isRu)
        => isRu ? def.LabelRu : def.Label;

    public static string GetCategoryLabel(string categoryKey, bool isRu)
        => Loc.Instance[$"BoostCat_{categoryKey}"];
}
