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
                        funcParams[0] = new ConditionParam { Name = "damageType", Type = "enum", EnumValues = DamageTypes };

                    // Special case: HasStatusGroup → StatusGroups enum
                    if (func.Value.name == "HasStatusGroup" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "statusGroup", Type = "enum", EnumValues = ConditionSchema.StatusGroups };

                    // Special case: HasSpellFlag → SpellFlags enum
                    if (func.Value.name == "HasSpellFlag" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "spellFlag", Type = "enum", EnumValues = SpellFlags };

                    // Special case: WieldingWeapon weaponFlags → WeaponProperties enum
                    if (func.Value.name == "WieldingWeapon" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "weaponFlags", Type = "enum", EnumValues = BoostMapping.WeaponFlags };

                    // Special case: HasActionResource resourceType → ActionResources enum
                    if (func.Value.name == "HasActionResource" && funcParams.Count > 0)
                        funcParams[0] = new ConditionParam { Name = "resourceType", Type = "enum", EnumValues = BoostMapping.ActionResources };

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
                    });
                }
            }

            // Special case overrides for khn functions with wrong param types
            if (name is "HasDamageDoneForType" or "HasDamageDoneForTypeIncludingZero"
                or "HasAttackDamageDoneForType" or "SpellDamageTypeIs" && funcParams.Count > 0)
                funcParams[0] = new ConditionParam { Name = "damageType", Type = "enum", EnumValues = DamageTypes };
            if (name == "HasStatusGroup" && funcParams.Count > 0)
                funcParams[0] = new ConditionParam { Name = "statusGroup", Type = "enum", EnumValues = StatusGroups };

            // Distance functions: value is float, not int
            if (name.StartsWith("DistanceTo") && funcParams.Count > 0)
                for (int fi = 0; fi < funcParams.Count; fi++)
                    if (funcParams[fi].Name == "value")
                        funcParams[fi] = new ConditionParam { Name = "distance", Type = "float" };

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
            Params = [new ConditionParam { Name = "weaponType", Type = "enum", EnumValues = BoostMapping.WeaponFlags }],
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
        "KhnAbility" => "enum",
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
        _ when name.StartsWith("Is") => "Check",
        _ when name.StartsWith("Has") => "Check",
        _ => "General"
    };
}
