using System.Reflection;
using System.Text.RegularExpressions;

namespace ParaTool.Core.Schema;

/// <summary>
/// Parsed condition function definition — name, parameters with types.
/// Built from Khn.HardcodedConditions.lua, CommonConditions.khn,
/// CommonConditionsDev.khn, and amp_conditions.khn.
/// </summary>
public sealed class ConditionDef
{
    public required string Name { get; init; }
    public string? Label { get; init; }       // English label
    public string? LabelRu { get; init; }     // Russian label
    public string Category { get; init; } = "General";
    public ConditionParam[] Params { get; init; } = [];

    /// <summary>True if this is a hardcoded C++ function (from HardcodedConditions.lua).</summary>
    public bool IsHardcoded { get; init; }

    /// <summary>Source file: "hardcoded", "common", "commondev", "amp".</summary>
    public string Source { get; init; } = "common";
}

public sealed class ConditionParam
{
    public required string Name { get; init; }
    public required string Type { get; init; } // "string", "enum", "flags", "int", "float", "bool", "entity"
    public string[]? EnumValues { get; init; }
    /// <summary>Optional short display labels (same length as EnumValues).</summary>
    public string[]? DisplayValues { get; init; }
    public bool IsOptional { get; init; }

    /// <summary>
    /// Enum namespace BG3 requires for this argument ("Ability.", "DamageType.", …).
    /// Non-null means the argument is written UNQUOTED and dotted — SavingThrow(Ability.Constitution,13).
    /// Null means a plain string literal — InSurface('SurfaceFire').
    /// </summary>
    public string? Prefix { get; init; }
}

/// <summary>
/// Complete BG3 condition function schema — loaded once from embedded resources.
/// Provides autocomplete, parameter types, and chip definitions for the Condition editor.
/// </summary>
public sealed partial class ConditionSchema
{
    private static ConditionSchema? _instance;
    private static readonly object _lock = new();

    public List<ConditionDef> Functions { get; } = [];
    public Dictionary<string, ConditionDef> ByName { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static ConditionSchema Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (_lock)
            {
                _instance ??= Load();
            }
            return _instance;
        }
    }

    [GeneratedRegex(@"\bHasNoTags\(")]
    private static partial Regex HasNoTagsRegex();
    [GeneratedRegex(@"\bHasAnyTags\(")]
    private static partial Regex HasAnyTagsRegex();

    /// <summary>
    /// Rewrite tag-list conditions into the proven Tagged() form. BG3's HasNoTags(tagList,target)
    /// and HasAnyTags(tagList,target) expect a table; ParaTool's chips emit a single tag, which the
    /// engine silently mis-evaluates (the condition never gates — reported as "Has No Tags SORCERER
    /// does nothing"). AMP itself always uses ~Tagged (= not Tagged) for this, never HasNoTags.
    /// For a single tag: HasNoTags('X') ≡ not Tagged('X'); HasAnyTags('X') ≡ Tagged('X').
    /// The args (tag + optional target entity) carry over unchanged since Tagged(tag,target) has
    /// the same shape. A rare "not HasNoTags(...)" double-negative is collapsed.
    /// </summary>
    public static string NormalizeTagConditions(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return condition ?? "";
        var result = HasNoTagsRegex().Replace(condition, "not Tagged(");
        result = HasAnyTagsRegex().Replace(result, "Tagged(");
        while (result.Contains("not not "))
            result = result.Replace("not not ", "");
        return result;
    }

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s*\(")]
    private static partial Regex CallRegex();

    /// <summary>
    /// Repair condition calls that older builds wrote in the wrong dialect, e.g.
    /// SavingThrow('Constitution','13',true,true) → SavingThrow(Ability.Constitution,13,true,true).
    /// BG3 reads a quoted 'Constitution' as a string, not as the Ability enum, so the whole
    /// condition silently evaluated to nothing and the IF never fired. Runs at compile time so
    /// artifacts saved by earlier versions are fixed without the user re-touching every chip.
    /// </summary>
    public static string NormalizeConditionEnums(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var schema = Instance;
        var sb = new System.Text.StringBuilder(text.Length);
        int pos = 0;

        foreach (Match m in CallRegex().Matches(text))
        {
            if (m.Index < pos) continue; // inside an already-rewritten call
            var name = m.Groups[1].Value;
            if (!schema.ByName.TryGetValue(name, out var def) || def.Params.Length == 0) continue;
            // Names shared with a boost/functor are ambiguous here — leave them alone.
            if (BoostMapping.FindBoost(name) != null || BoostMapping.FindFunctor(name) != null) continue;

            var open = m.Index + m.Length - 1;
            var close = MatchParen(text, open);
            if (close < 0) continue;

            var args = SplitTopLevel(text[(open + 1)..close]);
            if (args.Length == 0 || args.Length > def.Params.Length) continue;

            // Same leading-optional-entity shift the chip editor applies.
            int offset = 0;
            if (args.Length < def.Params.Length && def.Params[0].IsOptional && IsEntityParam(def.Params[0])
                && !args[0].Trim().Trim('\'', '"').StartsWith("context.", StringComparison.OrdinalIgnoreCase))
                offset = 1;

            var fixedArgs = args.Select((a, i) =>
                RepairArg((i + offset) < def.Params.Length ? def.Params[i + offset] : null, a));

            sb.Append(text, pos, open + 1 - pos);
            sb.Append(string.Join(",", fixedArgs));
            pos = close;
        }

        sb.Append(text, pos, text.Length - pos);
        return sb.ToString();
    }

    /// <summary>
    /// Conservative single-argument repair: only fixes what is unambiguously wrong —
    /// a known enum member missing its namespace, or a number/bool wrapped in quotes.
    /// Anything it doesn't recognise is returned byte-for-byte, so a hand-written
    /// expression or a mis-detected parameter can never be mangled.
    /// </summary>
    private static string RepairArg(ConditionParam? param, string original)
    {
        if (param == null) return original;
        var bare = original.Trim().Trim('\'', '"').Trim();
        if (bare.Length == 0 || bare.StartsWith("context.", StringComparison.OrdinalIgnoreCase)) return original;

        if (param.Type is "enum" or "flags" && param.Prefix != null && param.EnumValues != null)
        {
            var parts = bare.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (parts.All(v => v.Contains('.') || param.EnumValues.Contains(v, StringComparer.OrdinalIgnoreCase)))
                return string.Join(";", parts.Select(v => v.Contains('.') ? v : param.Prefix + v));
            return original;
        }

        if (param.Type is "int" or "float"
            && double.TryParse(bare, System.Globalization.NumberStyles.Any,
                               System.Globalization.CultureInfo.InvariantCulture, out _))
            return bare;

        if (param.Type == "bool" && bare is "true" or "false") return bare;

        return original;
    }

    private static int MatchParen(string s, int openIdx)
    {
        int depth = 0;
        for (int i = openIdx; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static string[] SplitTopLevel(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return [];
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == '(') depth++;
            else if (args[i] == ')') depth--;
            else if (args[i] == ',' && depth == 0) { parts.Add(args[start..i]); start = i + 1; }
        }
        parts.Add(args[start..]);
        return parts.ToArray();
    }

    // ── Known enum types for typed parameters ──────────────────

    public static readonly string[] Abilities = ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];

    public static readonly string[] Skills = BoostMapping.SkillType;

    public static readonly string[] DamageTypes = BoostMapping.DamageTypes;

    public static readonly string[] SurfaceTypes = BoostMapping.SurfaceTypes;

    public static readonly string[] SpellSchools = ["Abjuration", "Conjuration", "Divination", "Enchantment", "Evocation", "Illusion", "Necromancy", "Transmutation"];

    public static readonly string[] WeaponProperties = BoostMapping.WeaponFlags;

    public static readonly string[] StatusGroups =
    [
        "SG_Condition", "SG_Blinded", "SG_Charmed", "SG_Cursed", "SG_Disease",
        "SG_Frightened", "SG_Invisible", "SG_Poisoned", "SG_Restrained", "SG_Stunned",
        "SG_Polymorph", "SG_Paralyzed", "SG_Petrified", "SG_Rage", "SG_Taunted",
        "SG_Dominated", "SG_Confused", "SG_Mad", "SG_HexbladeCurse", "SG_Sleeping",
        "SG_Prone", "SG_Unconscious", "SG_Silenced", "SG_Incapacitated",
        "SG_Drunk", "SG_Exhausted", "SG_Dazed",
    ];

    public static readonly string[] SpellFlags =
    [
        "Spell", "Cantrip", "Melee", "Ranged", "HasHighGroundRangeExtension",
        "IsConcentration", "HasVerbalComponent", "HasSomaticComponent",
    ];

    public static readonly string[] ItemSlots = BoostMapping.StatItemSlot;

    public static readonly string[] SpellCategories =
    [
        "SpellCategory.Dash", "SpellCategory.Jump", "SpellCategory.DetectThoughts",
        "SpellCategory.None", "SpellCategory.TargetSingle", "SpellCategory.TargetMultiselect",
        "SpellCategory.TargetAoE", "SpellCategory.IntentDamage", "SpellCategory.IntentHealing",
        "SpellCategory.IntentBuff", "SpellCategory.IntentDebuff", "SpellCategory.IntentUtility",
    ];

    public static readonly string[] SpellTypes =
    [
        "SpellType.Damage", "SpellType.Healing", "SpellType.Rush", "SpellType.Shout",
        "SpellType.Zone", "SpellType.Throw", "SpellType.Wall", "SpellType.Teleportation",
        "SpellType.MultiStrike",
    ];

    public static readonly string[] InstrumentTypes =
    [
        "None", "Bagpipes", "Drum", "Dulcimer", "Flute", "Lute", "Lyre", "Horn", "Shawm", "Violin",
    ];

    public static readonly string[] HealingTypes = ["Healing", "HealSelf", "HealSharing"];

    public static readonly string[] StatusRemoveCauses = ["None", "Death", "LongRest", "ShortRest", "Expired"];

    public static readonly string[] SizeCategories = BoostMapping.SizeCategories;

    public static readonly string[] DamageFlags = ["Hit", "Miss", "Critical", "Magical", "NonLethal", "Melee", "Ranged", "WeaponBasedDamage", "Surface", "Projectile", "Trap", "Thorns"];

    public static readonly string[] EntityTargetsEn = ["Target", "Source"];
    public static readonly string[] EntityTargetsRu = ["Цель", "Источник"];

    public static string[] GetEntityTargets(bool russian) => russian ? EntityTargetsRu : EntityTargetsEn;

    public static string EntityToRaw(string display) => display switch
    {
        "Target" or "Цель" => "context.Target",
        "Source" or "Источник" => "context.Source",
        _ => display.Contains('.') ? display : $"context.{display}"
    };

    public static string EntityFromRaw(string raw, bool russian = false) => raw switch
    {
        "context.Target" => russian ? "Цель" : "Target",
        "context.Source" => russian ? "Источник" : "Source",
        _ => raw.Replace("context.", "")
    };

    public static readonly string[] InSurfaceValues =
    [
        "SurfaceNone", "SurfaceWater", "SurfaceWaterElectrified", "SurfaceWaterFrozen",
        "SurfaceBlood", "SurfaceBloodElectrified", "SurfaceBloodFrozen",
        "SurfacePoison", "SurfaceOil", "SurfaceLava", "SurfaceGrease",
        "SurfaceWeb", "SurfaceDeepwater", "SurfaceFire", "SurfaceAcid",
        "SurfaceMud", "SurfaceAlcohol", "SurfaceHellfire", "SurfaceAsh",
        "SurfaceSpikeGrowth", "SurfaceHolyFire", "SurfaceBlackTentacles",
        "SurfaceOvergrowth", "SurfaceWaterCloud", "SurfaceWaterCloudElectrified",
        "SurfacePoisonCloud", "SurfaceCloudkillCloud", "SurfaceDarknessCloud",
        "SurfaceFogCloud", "SurfaceIceCloud", "SurfaceSentinel",
        "SurfaceBladeBarrier", "SurfaceCausticBrine",
        "SurfaceWaterDeepRunning", "SurfaceWaterRunning",
        "SurfaceSurfaceDeepWater", "SurfaceSurfaceDeepWaterRunning",
        "SurfaceWaterElectrified", "SurfaceSurfaceWaterElectrified",
    ];

    /// <summary>Short display labels for InSurfaceValues (strip "Surface" prefix).</summary>
    public static readonly string[] InSurfaceLabels =
        InSurfaceValues.Select(s => s.StartsWith("Surface") ? s[7..] : s).ToArray();

    // ── Parsing ────────────────────────────────────────────────

    private static ConditionSchema Load()
    {
        var schema = new ConditionSchema();
        var asm = Assembly.GetExecutingAssembly();

        // 1. Hardcoded conditions (typed @param annotations)
        ParseHardcoded(schema, asm, "ParaTool.Core.Resources.Schema.Khn.HardcodedConditions.lua");

        // 2. CommonConditions.khn (Lua function defs)
        ParseKhn(schema, asm, "ParaTool.Core.Resources.Schema.CommonConditions.khn", "common");

        // 3. CommonConditionsDev.khn
        ParseKhn(schema, asm, "ParaTool.Core.Resources.Schema.CommonConditionsDev.khn", "commondev");

        // 4. AMP conditions
        ParseKhn(schema, asm, "ParaTool.Core.Resources.Schema.amp_conditions.khn", "amp");

        // 5. BG3 built-in conditions not in khn files (used in BoostConditions)
        RegisterBuiltinConditions(schema);

        return schema;
    }

    /// <summary>
    /// Parse HardcodedConditions.lua — extracts @param annotations for typed parameters.
    /// Format: ---@param name Type \n function FuncName(params) end
    /// </summary>
    private static void ParseHardcoded(ConditionSchema schema, Assembly asm, string resource)
    {
        using var stream = asm.GetManifestResourceStream(resource);
        if (stream == null) return;
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        var lines = text.Split('\n');

        var paramAnnotations = new List<(string name, string type)>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.StartsWith("---@param "))
            {
                // ---@param name Type
                var parts = line[10..].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    paramAnnotations.Add((parts[0], parts[1]));
            }
            else if (line.StartsWith("---@overload") || line.StartsWith("---@return") || line.StartsWith("---@diagnostic"))
            {
                // skip
            }
            else if (line.StartsWith("function "))
            {
                var func = ParseFunctionLine(line);
                if (func != null)
                {
                    // Map @param annotations to actual parameters
                    var funcParams = new List<ConditionParam>();
                    foreach (var (pName, pType) in paramAnnotations)
                    {
                        // Vector params are internal — skip
                        if (pType is "Khn_Vector") continue;

                        funcParams.Add(new ConditionParam
                        {
                            Name = pName,
                            Type = MapLuaType(pType),
                            EnumValues = GetEnumValues(pType),
                            Prefix = GetEnumPrefix(pType),
                            // Entity params are optional — BG3 auto-fills from context
                            IsOptional = pType is "Khn_Entity",
                        });
                    }

                    // Special case: InSurface gridStateStr → surface enum
                    if (func.Value.name == "InSurface" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "surface", Type = "enum", EnumValues = InSurfaceValues };

                    // Special case: DamageType params named "value", "damageType" etc.
                    if (func.Value.name is "HasAttackDamageDoneForType" or "HasDamageDoneForType"
                        or "HasDamageDoneForTypeIncludingZero" or "SpellDamageTypeIs" or "HasDamageEffectFlag"
                        && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "damageType", Type = "enum", EnumValues = DamageTypes, Prefix = "DamageType." };

                    // Special case: HasStatusGroup → StatusGroups enum
                    if (func.Value.name == "HasStatusGroup" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "statusGroup", Type = "enum", EnumValues = ConditionSchema.StatusGroups };

                    // Special case: HasSpellFlag → SpellFlags enum
                    if (func.Value.name == "HasSpellFlag" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "spellFlag", Type = "enum", EnumValues = SpellFlags, Prefix = "SpellFlags." };

                    // Special case: WieldingWeapon weaponFlags → WeaponProperties enum
                    if (func.Value.name == "WieldingWeapon" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "weaponFlags", Type = "enum", EnumValues = BoostMapping.WeaponFlags, Prefix = "WeaponProperties." };

                    // Special case: HasActionResource resourceType → ActionResources enum
                    if (func.Value.name == "HasActionResource" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "resourceType", Type = "enum", EnumValues = BoostMapping.ActionResources };

                    // Trailing advantage/disadvantage-style booleans are optional in BG3 and
                    // default to false. Emitting them unasked produced nonsense like
                    // SavingThrow(...,true,true) — advantage AND disadvantage at once.
                    MarkTrailingFlagsOptional(funcParams);

                    AddFunc(schema, new ConditionDef
                    {
                        Name = func.Value.name,
                        Params = funcParams.ToArray(),
                        IsHardcoded = true,
                        Source = "hardcoded",
                        Category = CategorizeCondition(func.Value.name),
                    });
                }
                paramAnnotations.Clear();
            }
            else
            {
                // Non-annotation, non-function line — reset annotations
                if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("--"))
                    paramAnnotations.Clear();
            }
        }
    }

    /// <summary>
    /// Walk back from the last parameter and mark every trailing boolean (and entity) as
    /// optional, so a freshly added chip only asks for the arguments BG3 actually requires.
    /// Shipped stats confirm the shape: SkillCheck(Skill.Stealth,15) and
    /// SavingThrow(Ability.Constitution,13) — the flags only appear when someone needs them.
    /// </summary>
    private static void MarkTrailingFlagsOptional(List<ConditionParam> funcParams)
    {
        for (int i = funcParams.Count - 1; i >= 0; i--)
        {
            var p = funcParams[i];
            if (p.IsOptional) continue;                       // entity — already optional
            if (p.Type != "bool") break;                      // hit a required arg — stop
            funcParams[i] = new ConditionParam
            {
                Name = p.Name, Type = p.Type, EnumValues = p.EnumValues,
                DisplayValues = p.DisplayValues, Prefix = p.Prefix, IsOptional = true,
            };
        }
    }

    /// <summary>
    /// Parse .khn files — extract function Name(params) from Lua source.
    /// No type annotations — params are untyped (inferred from naming conventions).
    /// </summary>
    private static void ParseKhn(ConditionSchema schema, Assembly asm, string resource, string source)
    {
        using var stream = asm.GetManifestResourceStream(resource);
        if (stream == null) return;
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        foreach (Match m in Regex.Matches(text, @"^function\s+(\w+)\s*\(([^)]*)\)", RegexOptions.Multiline))
        {
            var name = m.Groups[1].Value;
            var argsStr = m.Groups[2].Value.Trim();

            // Skip internal helpers (start with lowercase, Get*, local helpers)
            if (char.IsLower(name[0]) && name != "context") continue;
            if (name.StartsWith("Get") && !name.StartsWith("GetModifier")) continue;

            // Already registered from hardcoded — skip
            if (schema.ByName.ContainsKey(name)) continue;

            var funcParams = new List<ConditionParam>();
            if (!string.IsNullOrEmpty(argsStr))
            {
                foreach (var arg in argsStr.Split(',', StringSplitOptions.TrimEntries))
                {
                    if (string.IsNullOrEmpty(arg)) continue;
                    // Entity params → optional Target/Source enum (BG3 auto-fills from context)
                    if (arg is "entity" or "entity2" or "target" or "source" or "owner")
                    {
                        funcParams.Add(new ConditionParam
                        {
                            Name = arg, Type = "enum", EnumValues = EntityTargetsEn, IsOptional = true,
                        });
                        continue;
                    }

                    funcParams.Add(new ConditionParam
                    {
                        Name = arg,
                        Type = InferTypeFromName(arg),
                        EnumValues = GetEnumValuesFromName(arg),
                        Prefix = GetEnumPrefixFromName(arg),
                    });
                }
            }

            // Special case overrides for khn functions with wrong param types
            if (name is "HasDamageDoneForType" or "HasDamageDoneForTypeIncludingZero"
                or "HasAttackDamageDoneForType" or "SpellDamageTypeIs" && funcParams.Count > 0)
                funcParams[0] = new ConditionParam { Name = "damageType", Type = "enum", EnumValues = DamageTypes, Prefix = "DamageType." };
            if (name == "HasStatusGroup" && funcParams.Count > 0)
                funcParams[0] = new ConditionParam { Name = "statusGroup", Type = "enum", EnumValues = StatusGroups };

            // Distance functions: value is float, not int
            if (name.StartsWith("DistanceTo") && funcParams.Count > 0)
                for (int fi = 0; fi < funcParams.Count; fi++)
                    if (funcParams[fi].Name == "value")
                        funcParams[fi] = new ConditionParam { Name = "distance", Type = "float" };

            MarkTrailingFlagsOptional(funcParams);

            AddFunc(schema, new ConditionDef
            {
                Name = name,
                Params = funcParams.ToArray(),
                Source = source,
                Category = CategorizeCondition(name),
            });
        }
    }

    /// <summary>Register commonly used BG3 conditions not found in khn files.
    /// Uses overwrite=true to replace khn-parsed definitions with properly typed params.</summary>
    private static void RegisterBuiltinConditions(ConditionSchema schema)
    {
        var statusParam = new ConditionParam { Name = "statusId", Type = "string" };
        var entityParam = new ConditionParam { Name = "target", Type = "enum", EnumValues = EntityTargetsEn, IsOptional = true };
        var intParam = new ConditionParam { Name = "amount", Type = "int" };

        // Status duration conditions: StatusDuration*(entity, statusId, amount)
        foreach (var name in new[] { "StatusDurationLessThan", "StatusDurationMoreThan",
            "StatusDurationEqualOrLessThan", "StatusDurationEqualOrMoreThan" })
        {
            AddFunc(schema, new ConditionDef
            {
                Name = name, Category = "Status", Source = "builtin",
                Params = [entityParam, statusParam, intParam],
            }, overwrite: true);
        }

        // HasStatusWithGroup(statusGroup, entity)
        AddFunc(schema, new ConditionDef
        {
            Name = "HasStatusWithGroup", Category = "Status", Source = "builtin",
            Params = [entityParam, new ConditionParam { Name = "statusGroup", Type = "enum", EnumValues = StatusGroups }],
        }, overwrite: true);

        // StatusStacksLessThan / MoreThan(entity, statusId, amount)
        foreach (var name in new[] { "StatusStacksLessThan", "StatusStacksMoreThan",
            "StatusStacksEqualOrLessThan", "StatusStacksEqualOrMoreThan" })
        {
            AddFunc(schema, new ConditionDef
            {
                Name = name, Category = "Status", Source = "builtin",
                Params = [entityParam, statusParam, intParam],
            }, overwrite: true);
        }

        // SpellAttackRollAbove / Below(amount)
        foreach (var name in new[] { "SpellAttackRollAbove", "SpellAttackRollBelow",
            "AttackRollAbove", "AttackRollBelow", "SavingThrowRollAbove", "SavingThrowRollBelow" })
        {
            AddFunc(schema, new ConditionDef
            {
                Name = name, Category = "Roll", Source = "builtin",
                Params = [intParam],
            }, overwrite: true);
        }

        // WieldingWeaponOfType(weaponType)
        AddFunc(schema, new ConditionDef
        {
            Name = "WieldingWeaponOfType", Category = "Item", Source = "builtin",
            Params = [new ConditionParam { Name = "weaponType", Type = "enum", EnumValues = BoostMapping.WeaponFlags, Prefix = "WeaponProperties." }],
        }, overwrite: true);

        // HasArmorType(armorType)
        AddFunc(schema, new ConditionDef
        {
            Name = "HasArmorType", Category = "Item", Source = "builtin",
            Params = [new ConditionParam { Name = "armorType", Type = "enum", EnumValues = BoostMapping.ArmorTypes }],
        }, overwrite: true);
    }

    private static void AddFunc(ConditionSchema schema, ConditionDef func, bool overwrite = false)
    {
        if (schema.ByName.ContainsKey(func.Name))
        {
            if (!overwrite) return;
            // Replace existing definition (builtin with typed params overrides khn-parsed untyped)
            var old = schema.ByName[func.Name];
            schema.Functions.Remove(old);
        }
        schema.Functions.Add(func);
        schema.ByName[func.Name] = func;
    }

    private static (string name, string[] args)? ParseFunctionLine(string line)
    {
        var m = Regex.Match(line, @"^function\s+(\w+)\s*\(([^)]*)\)");
        if (!m.Success) return null;
        var name = m.Groups[1].Value;
        var args = m.Groups[2].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return (name, args);
    }

    // ── Type mapping ───────────────────────────────────────────

    private static string MapLuaType(string luaType) => luaType switch
    {
        "string" => "string",
        "boolean" => "bool",
        "KhnFloat" => "float",
        "KhnInteger" => "int",
        // A DC — plain number or a formula like ManeuverSaveDC(). Numeric chip, never a quoted string.
        "RollOptions" => "int",
        "KhnAbility" => "enum",
        "KhnSkill" => "enum",
        "KhnAttackType" => "enum",
        "KhnDamageType" => "enum",
        "KhnSchool" => "enum",
        "KhnWeaponProperties" => "enum",
        "Khn_Entity" => "enum",
        "KhnInstrumentType" => "enum",
        "KhnHealingType" => "enum",
        "KhnStatusRemoveCause" => "enum",
        "KhnSpellCategory" => "enum",
        "SpellFlags" => "flags",
        "SpellType" => "enum",
        "StatsFunctorType" => "string",
        "DamageFlags" => "flags",
        "ItemSlot" => "enum",
        "table" => "string",
        _ => "string"
    };

    private static string[]? GetEnumValues(string luaType) => luaType switch
    {
        "KhnAbility" => Abilities,
        "KhnSkill" => Skills,
        "KhnAttackType" => BoostMapping.AttackType,
        "KhnDamageType" => DamageTypes,
        "KhnSchool" => SpellSchools,
        "KhnWeaponProperties" => WeaponProperties,
        "Khn_Entity" => EntityTargetsEn,
        "SpellFlags" => SpellFlags,
        "ItemSlot" => ItemSlots,
        "KhnSpellCategory" => SpellCategories,
        "SpellType" => SpellTypes,
        "KhnInstrumentType" => InstrumentTypes,
        "KhnHealingType" => HealingTypes,
        "KhnStatusRemoveCause" => StatusRemoveCauses,
        "DamageFlags" => DamageFlags,
        _ => null
    };

    /// <summary>
    /// Enum namespace BG3 expects for a Khn condition argument. Verified against shipped
    /// stats: SavingThrow(Ability.Constitution,13), HasDamageDoneForType(DamageType.Fire),
    /// SkillCheck(Skill.Stealth,15), HasSpellFlag(SpellFlags.Spell). Types absent from this
    /// map take plain quoted strings — InSurface('SurfaceFire'), HasActionResource('Ki',1,0).
    /// </summary>
    private static string? GetEnumPrefix(string luaType) => luaType switch
    {
        "KhnAbility" => "Ability.",
        "KhnSkill" => "Skill.",
        "KhnAttackType" => "AttackType.",
        "KhnDamageType" => "DamageType.",
        "KhnSchool" => "SpellSchool.",
        "KhnWeaponProperties" => "WeaponProperties.",
        "KhnStatusRemoveCause" => "StatusRemoveCause.",
        "ItemSlot" => "EquipmentSlot.",
        "SpellFlags" => "SpellFlags.",
        "DamageFlags" => "DamageFlags.",
        // SpellCategory/SpellType values already carry their namespace in the value itself —
        // they only need to stay unquoted, so an empty (non-null) prefix marks them dotted.
        "KhnSpellCategory" or "SpellType" => "",
        _ => null
    };

    private static string? GetEnumPrefixFromName(string paramName)
    {
        var lower = paramName.ToLowerInvariant();
        if (lower.StartsWith("ability")) return "Ability.";
        return lower switch
        {
            "skill" => "Skill.",
            "damagetype" or "dmgtype" => "DamageType.",
            "school" or "spellschool" => "SpellSchool.",
            "slot" => "EquipmentSlot.",
            "size" => "Size.",
            "attacktype" => "AttackType.",
            "actiontype" => "ActionType.",
            "conditionrolltype" => "ConditionRollType.",
            "properties" or "weaponflags" or "flags" => "WeaponProperties.",
            _ => null
        };
    }

    /// <summary>
    /// Render one argument the way BG3 wants to read it: dotted enums unquoted
    /// (Ability.Constitution), entities as context.X, numbers/bools bare, everything
    /// else as a quoted string literal.
    /// </summary>
    public static string FormatArg(ConditionParam? param, string value)
    {
        value = value.Trim().Trim('\'', '"').Trim();
        if (value.Length == 0) return "";

        // Already an entity or a call/expression — leave untouched.
        if (value.StartsWith("context.", StringComparison.OrdinalIgnoreCase) || value.Contains('('))
            return value;

        if (param == null) return $"'{value}'";

        switch (param.Type)
        {
            case "int" or "float" or "bool":
                return value;
            case "enum" or "flags":
                if (IsEntityParam(param))
                    return EntityTargetsEn.Contains(value) || EntityTargetsRu.Contains(value)
                        ? EntityToRaw(value) : $"'{value}'";
                if (param.Prefix == null) return $"'{value}'";
                // Split on ';' so multi-flag values get one prefix each.
                return string.Join(";", value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Contains('.') ? v : param.Prefix + v));
            default:
                return $"'{value}'";
        }
    }

    public static bool IsEntityParam(ConditionParam param) =>
        param.EnumValues == EntityTargetsEn || param.EnumValues == EntityTargetsRu;

    private static string InferTypeFromName(string paramName)
    {
        var lower = paramName.ToLowerInvariant();
        // Indexed abilities: ability1, ability2
        if (lower.StartsWith("ability")) return "enum";
        return lower switch
        {
            "damagetype" or "dmgtype" => "enum",
            "school" or "spellschool" => "enum",
            "slot" => "enum",
            "level" or "dc" or "basedc" or "fallbackdc"
                or "value" or "amount" or "cost" or "number"
                or "max" or "min" or "minvalue" or "maxvalue"
                or "grenadenum" or "slotnum" or "maxuses"
                or "numberofenemy" => "int",
            "distance" => "float",
            "offhand" or "checkranged" or "mainhand" or "ispercentage"
                or "result" or "checkstacks" or "spellcast" or "hasshield" => "bool",
            "resourcetype" or "resource" => "enum",
            "statusid" or "status" or "spellid" or "spell" or "passivename" or "tag" => "string",
            "attacktype" => "enum",
            "size" => "enum",
            "dicetype" => "enum",
            "actiontype" => "enum",
            "conditionrolltype" => "enum",
            "properties" or "weaponflags" or "flags" => "enum",
            _ => "string"
        };
    }

    private static string[]? GetEnumValuesFromName(string paramName)
    {
        var lower = paramName.ToLowerInvariant();
        if (lower.StartsWith("ability")) return Abilities;
        return lower switch
        {
            "damagetype" or "dmgtype" => DamageTypes,
            "school" or "spellschool" => SpellSchools,
            "slot" => ItemSlots,
            "properties" or "weaponflags" => WeaponProperties,
            "flags" => WeaponProperties,
            "size" => SizeCategories,
            "attacktype" => BoostMapping.AttackType,
            "resourcetype" or "resource" => BoostMapping.ActionResources,
            "dicetype" => ["D4", "D6", "D8", "D10", "D12", "D20"],
            "conditionrolltype" => ["ConditionSavingThrow", "ConditionAbilityCheck", "ConditionAttackRoll", "ConditionDeathSavingThrow"],
            "actiontype" => ["MainAction", "BonusAction", "ReAction", "FreeAction", "Movement"],
            _ => null
        };
    }

    // ── Categories ─────────────────────────────────────────────

    private static string CategorizeCondition(string name) => name switch
    {
        "Enemy" or "Ally" or "Self" or "Party" or "Player" or "Summon" => "Target",
        "Combat" or "TurnBased" or "ActedThisRoundInCombat" or "HadTurnInCombat" => "Combat",
        "Dead" or "IsDowned" or "LethalHP" or "FreshCorpse" => "State",
        "HasStatus" or "StatusId" or "HasAnyStatus" or "IsImmuneToStatus" or "StatusHasStatusGroup" or "HasExtendableStatus" => "Status",
        "IsWeaponAttack" or "IsRangedWeaponAttack" or "IsMeleeAttack" or "IsSpellAttack"
            or "IsRangedAttack" or "IsMeleeWeaponAttack" => "Attack",
        "IsCritical" or "IsMiss" or "IsCriticalMiss" or "IsHit" => "Roll",
        "IsSpell" or "IsCantrip" or "SpellId" or "IsSpellOfSchool" or "IsLeveledSpell"
            or "SpellTypeIs" or "SpellCategoryIs" or "HasSpellFlag" or "IsSpellLevel" => "Spell",
        "HasShieldEquipped" or "WearingArmor" or "IsEquipped" or "HasWeaponProperty"
            or "WieldingWeapon" or "Unarmed" or "IsWeapon" or "EquipmentSlotIs" => "Equipment",
        "InSurface" or "Grounded" => "Surface",
        "HasPassive" or "IsPassiveSource" or "IsPassiveOwner" => "Passive",
        "HasActionResource" or "HasUseCosts" => "Resource",
        "SavingThrow" or "SkillCheck" or "RollDieAgainstDC" => "Roll",
        "Tagged" or "HasAnyTags" or "HasNoTags" => "Tag",
        // AMP custom conditions
        "IsSneakAttack" or "IsDischargingLightning" or "IsLeveledSpell" or "IsLeveledSpellStrict"
            or "IsGameplayStatus" or "IsSpellIdOrChild" or "AttackedWithPassiveSourceWeapon" => "AMP",
        _ when name.StartsWith("Is") => "Check",
        _ when name.StartsWith("Has") => "Check",
        _ => "General"
    };
}
