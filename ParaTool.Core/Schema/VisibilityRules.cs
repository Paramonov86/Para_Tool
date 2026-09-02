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

    /// <summary>
    /// True if the given param participates in a visibility rule. Used by the chip
    /// renderer to force-render context-dependent slots (instead of skipping them
    /// as "trailing empty optional") when the governing arg currently enables them.
    /// </summary>
    public static bool HasRule(BoostMapping.BlockDef def, int paramIdx)
    {
        if (!_rules.TryGetValue(def.FuncName, out var rules)) return false;
        foreach (var r in rules)
            if (r.DependentParamIdx == paramIdx) return true;
        return false;
    }

    // ── Dependent enums ─────────────────────────────────────
    // Some boosts take a "kind" arg that decides which vocabulary the NEXT arg speaks.
    // BG3 silently drops the whole boost when the two disagree — Advantage(Skill,Intelligence)
    // reads fine in the editor but never fires in game, because Skill wants a skill name and
    // Intelligence is an ability. The tumbler must only offer values the governing arg allows.
    public record EnumNarrowRule(int GovernIdx, int DependentIdx, Dictionary<string, string[]> ByGoverningValue);

    private static readonly Dictionary<string, string[]> AbilityOrSkillByKind = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SavingThrow"]    = BoostMapping.Abilities,
        ["Ability"]        = BoostMapping.Abilities,
        ["RawAbility"]     = BoostMapping.Abilities,
        ["SourceDialogue"] = BoostMapping.Abilities,
        ["Skill"]          = BoostMapping.SkillType,
        ["SkillCheck"]     = BoostMapping.SkillType,
    };

    private static readonly Dictionary<string, EnumNarrowRule[]> _enumNarrowing = new()
    {
        ["Advantage"]        = [new(0, 1, AbilityOrSkillByKind)],
        ["Disadvantage"]     = [new(0, 1, AbilityOrSkillByKind)],
        ["ProficiencyBonus"] = [new(0, 1, AbilityOrSkillByKind)],
        ["RollBonus"]        = [new(0, 2, AbilityOrSkillByKind)],
    };

    /// <summary>
    /// The values a param may actually take given the current args, or null when the param
    /// isn't context-dependent (caller then uses the param's own EnumValues).
    /// </summary>
    public static string[]? NarrowEnum(BoostMapping.BlockDef def, int paramIdx, string[] args)
    {
        if (!_enumNarrowing.TryGetValue(def.FuncName, out var rules)) return null;
        foreach (var r in rules)
        {
            if (r.DependentIdx != paramIdx) continue;
            if (args.Length <= r.GovernIdx) return null;
            if (r.ByGoverningValue.TryGetValue(args[r.GovernIdx].Trim(), out var allowed))
                return allowed;
        }
        return null;
    }

    /// <summary>
    /// Repair args whose dependent value belongs to the wrong vocabulary. Applied at compile
    /// time so artifacts saved before the tumbler was constrained still produce working boosts:
    /// Advantage(Skill,Intelligence) becomes Advantage(Ability,Intelligence).
    /// Returns true when something was corrected.
    /// </summary>
    public static bool FixDependentEnum(string funcName, string[] args)
    {
        if (!_enumNarrowing.TryGetValue(funcName, out var rules)) return false;
        var fixedAny = false;
        foreach (var r in rules)
        {
            if (args.Length <= r.GovernIdx || args.Length <= r.DependentIdx) continue;
            var kind = args[r.GovernIdx].Trim();
            var value = args[r.DependentIdx].Trim();
            if (value.Length == 0) continue;
            if (!r.ByGoverningValue.TryGetValue(kind, out var allowed)) continue;
            if (allowed.Contains(value, StringComparer.OrdinalIgnoreCase)) continue;

            // Value speaks the other vocabulary — swap the governing arg to match it, keeping
            // the user's intent (they picked the ability/skill deliberately).
            var isAbility = BoostMapping.Abilities.Contains(value, StringComparer.OrdinalIgnoreCase);
            var isSkill = BoostMapping.SkillType.Contains(value, StringComparer.OrdinalIgnoreCase);
            string? correctedKind = null;
            if (isAbility && ReferenceEquals(allowed, BoostMapping.SkillType))
                correctedKind = kind.Equals("SkillCheck", StringComparison.OrdinalIgnoreCase) ? "RawAbility" : "Ability";
            else if (isSkill && ReferenceEquals(allowed, BoostMapping.Abilities))
                correctedKind = funcName.Equals("RollBonus", StringComparison.OrdinalIgnoreCase) ? "SkillCheck" : "Skill";
            if (correctedKind == null) continue;

            args[r.GovernIdx] = correctedKind;
            fixedAny = true;
        }
        return fixedAny;
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
