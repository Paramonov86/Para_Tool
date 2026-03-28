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
    public required string Type { get; init; } // "string", "enum", "int", "float", "bool", "entity", "ability", "damageType", "surface", etc.
    public string[]? EnumValues { get; init; }
    public bool IsOptional { get; init; }
}

/// <summary>
/// Complete BG3 condition function schema — loaded once from embedded resources.
/// Provides autocomplete, parameter types, and chip definitions for the Condition editor.
/// </summary>
public sealed class ConditionSchema
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

    // ── Known enum types for typed parameters ──────────────────

    public static readonly string[] Abilities = ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];

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
                        // Skip entity/source/target params — user doesn't set these
                        if (pType is "Khn_Entity" or "Khn_Vector") continue;

                        funcParams.Add(new ConditionParam
                        {
                            Name = pName,
                            Type = MapLuaType(pType),
                            EnumValues = GetEnumValues(pType),
                            IsOptional = false,
                        });
                    }

                    // Special case: InSurface gridStateStr → surface enum
                    if (func.Value.name == "InSurface" && funcParams.Count > 0)
                    {
                        funcParams[0] = new ConditionParam { Name = "surface", Type = "enum", EnumValues = InSurfaceValues };
                    }

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
                    // Skip entity/context params
                    if (arg is "entity" or "entity2" or "target" or "source" or "owner") continue;

                    funcParams.Add(new ConditionParam
                    {
                        Name = arg,
                        Type = InferTypeFromName(arg),
                        EnumValues = GetEnumValuesFromName(arg),
                    });
                }
            }

            AddFunc(schema, new ConditionDef
            {
                Name = name,
                Params = funcParams.ToArray(),
                Source = source,
                Category = CategorizeCondition(name),
            });
        }
    }

    private static void AddFunc(ConditionSchema schema, ConditionDef func)
    {
        if (schema.ByName.ContainsKey(func.Name)) return;
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
        "KhnAbility" => "enum",
        "KhnDamageType" => "enum",
        "KhnSchool" => "enum",
        "KhnWeaponProperties" => "enum",
        "KhnInstrumentType" => "string",
        "KhnHealingType" => "string",
        "KhnStatusRemoveCause" => "string",
        "KhnSpellCategory" => "string",
        "SpellFlags" => "enum",
        "SpellType" => "string",
        "StatsFunctorType" => "string",
        "DamageFlags" => "string",
        "ItemSlot" => "enum",
        "table" => "string",
        _ => "string"
    };

    private static string[]? GetEnumValues(string luaType) => luaType switch
    {
        "KhnAbility" => Abilities,
        "KhnDamageType" => DamageTypes,
        "KhnSchool" => SpellSchools,
        "KhnWeaponProperties" => WeaponProperties,
        "SpellFlags" => SpellFlags,
        "ItemSlot" => ItemSlots,
        _ => null
    };

    private static string InferTypeFromName(string paramName) => paramName.ToLowerInvariant() switch
    {
        "ability" => "enum",
        "damagetype" or "dmgtype" => "enum",
        "school" => "enum",
        "slot" => "enum",
        "level" or "dc" or "basedc" or "value" or "amount" or "cost" => "int",
        "offhand" or "checkranged" or "mainhand" or "ispercentage" => "bool",
        "statusid" or "status" or "spellid" or "spell" or "passivename" or "tag" => "string",
        "size" => "string",
        "properties" or "weaponflags" or "flags" => "enum",
        _ => "string"
    };

    private static string[]? GetEnumValuesFromName(string paramName) => paramName.ToLowerInvariant() switch
    {
        "ability" => Abilities,
        "damagetype" or "dmgtype" => DamageTypes,
        "school" => SpellSchools,
        "slot" => ItemSlots,
        "properties" or "weaponflags" => WeaponProperties,
        _ => null
    };

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
        _ when name.StartsWith("Is") => "Check",
        _ when name.StartsWith("Has") => "Check",
        _ => "General"
    };
}
