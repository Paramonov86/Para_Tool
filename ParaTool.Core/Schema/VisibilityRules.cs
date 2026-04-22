namespace ParaTool.Core.Schema;

/// <summary>
/// Centralised conditional-visibility rules for chip parameters.
/// Single source of truth — read by BoostBlocksEditor.CreateBlock, OnAddClick, UpdateParam.
/// When a param is "hidden" by context, its value must also be cleared so it doesn't
/// leak into compiled stats output (BG3 silently rejects boosts with garbage args).
/// </summary>
public static class VisibilityRules
{
    public record Rule(int FirstArgIdx, HashSet<string> EnablingValues, int DependentParamIdx);

    private static readonly Dictionary<string, Rule[]> _rules = new()
    {
        // RollBonus: 3rd arg (Ability/Skill) only when rolling SavingThrow/SkillCheck/RawAbility
        ["RollBonus"]              = [new(0, new(["SavingThrow", "SkillCheck", "RawAbility"]), 2)],

        // Advantage/Disadvantage: 2nd arg only for SavingThrow/Ability/Skill contexts
        ["Advantage"]              = [new(0, new(["SavingThrow", "Ability", "Skill"]), 1)],
        ["Disadvantage"]           = [new(0, new(["SavingThrow", "Ability", "Skill"]), 1)],

        // Ability/AbilityOverrideMinimum: Savant (optbool) only for Constitution
        ["Ability"]                = [new(0, new(["Constitution"]), 3)],
        ["AbilityOverrideMinimum"] = [new(0, new(["Constitution"]), 2)],

        // DamageReduction: Amount (3rd arg) only for Flat/Threshold; hidden when Half
        ["DamageReduction"]        = [new(1, new(["Flat", "Threshold"]), 2)],
    };

    public static bool IsHidden(BoostMapping.BlockDef def, int paramIdx, string[] args)
    {
        if (!_rules.TryGetValue(def.FuncName, out var rules)) return false;
        foreach (var r in rules)
        {
            if (r.DependentParamIdx != paramIdx) continue;
            if (args.Length <= r.FirstArgIdx) return true;
            var firstVal = args[r.FirstArgIdx].Trim();
            if (!r.EnablingValues.Contains(firstVal)) return true;
        }
        return false;
    }

    public static string[] ClearHiddenArgs(BoostMapping.BlockDef def, string[] args)
    {
        if (!_rules.TryGetValue(def.FuncName, out var rules) || args.Length == 0)
            return args;

        var result = (string[])args.Clone();
        foreach (var r in rules)
        {
            if (result.Length <= r.FirstArgIdx) continue;
            var firstVal = result[r.FirstArgIdx].Trim();
            if (!r.EnablingValues.Contains(firstVal)
                && r.DependentParamIdx < result.Length)
            {
                result[r.DependentParamIdx] = "";
            }
        }
        return result;
    }
}
