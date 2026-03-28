namespace ParaTool.Core.Schema;

/// <summary>
/// Complete mapping of BG3 Stats syntax → visual block definitions for child-friendly UI.
/// Converts raw code like "AC(2)" into colored blocks like [Armor Class] [2].
/// </summary>
public static class BoostMapping
{
    public record BlockDef(string FuncName, string Label, string LabelRu, string Color, ParamDef[] Params);
    public record ParamDef(string Name, string Label, string Type, string[]? EnumValues = null);
    // Type: "number", "string", "enum", "dice", "formula"

    // ═══════ BOOSTS ═══════

    public static readonly BlockDef[] Boosts =
    [
        new("AC", "Armor Class", "Класс брони", "#2ECC71", [new("value", "+/-", "number")]),
        new("Ability", "Ability Score", "Показатель способности", "#2ECC71",
            [new("ability", "Ability", "enum", Abilities), new("bonus", "+/-", "number")]),
        new("AbilityOverrideMinimum", "Minimum Ability", "Минимум способности", "#2ECC71",
            [new("ability", "Ability", "enum", Abilities), new("min", "Min", "number")]),
        new("Initiative", "Initiative", "Инициатива", "#3498DB", [new("bonus", "+/-", "number")]),
        new("WeaponEnchantment", "Weapon Enchantment", "Зачарование оружия", "#3498DB", [new("level", "+", "number")]),
        new("WeaponProperty", "Weapon Property", "Свойство оружия", "#3498DB",
            [new("prop", "Property", "enum", WeaponProperties)]),
        new("Resistance", "Resistance", "Сопротивление", "#F1C40F",
            [new("dmgType", "Damage", "enum", DamageTypes), new("level", "Level", "enum", ResistanceLevels)]),
        new("StatusImmunity", "Status Immunity", "Иммунитет к статусу", "#9B59B6", [new("status", "Status", "string")]),
        new("Skill", "Skill Bonus", "Бонус к навыку", "#3498DB",
            [new("skill", "Skill", "enum", Skills), new("bonus", "+/-", "number")]),
        new("RollBonus", "Attack Bonus", "Бонус к атаке", "#E67E22",
            [new("type", "Type", "enum", AttackTypes), new("bonus", "+/-", "dice")]),
        new("DamageBonus", "Damage Bonus", "Бонус к урону", "#E74C3C",
            [new("dice", "Dice", "dice"), new("type", "Type", "enum", DamageTypes)]),
        new("SpellSaveDC", "Spell Save DC", "DC заклинания", "#9B59B6", [new("bonus", "+/-", "number")]),
        new("UnlockSpell", "Unlock Spell", "Открыть заклинание", "#9B59B6", [new("spell", "Spell", "string")]),
        new("IncreaseMaxHP", "Max HP", "Макс. ОЗ", "#E74C3C", [new("amount", "+", "formula")]),
        new("TemporaryHP", "Temporary HP", "Врем. ОЗ", "#E74C3C", [new("amount", "Amount", "formula")]),
        new("Advantage", "Advantage", "Преимущество", "#2ECC71",
            [new("type", "On", "enum", AdvantageTypes)]),
        new("Disadvantage", "Disadvantage", "Помеха", "#E67E22",
            [new("type", "On", "enum", AdvantageTypes)]),
        new("Proficiency", "Proficiency", "Владение", "#3498DB",
            [new("type", "Type", "enum", ProficiencyTypes)]),
        new("ProficiencyBonus", "Proficiency Bonus", "Бонус владения", "#3498DB",
            [new("type", "Type", "enum", ["Skill", "Weapon"]), new("name", "Name", "string")]),
        new("CriticalHitExtraDice", "Extra Crit Dice", "Доп. кубики крита", "#E74C3C",
            [new("amount", "Dice", "number")]),
        new("ReduceCriticalAttackThreshold", "Lower Crit Threshold", "Снизить порог крита", "#E74C3C",
            [new("value", "By", "number")]),
        new("ActionResource", "Action Resource", "Ресурс действия", "#9B59B6",
            [new("type", "Resource", "enum", ActionResources), new("amount", "+/-", "number"), new("level", "Level", "number")]),
        new("DarkvisionRangeMin", "Darkvision", "Тёмное зрение", "#8A8494", [new("range", "Range", "number")]),
        new("JumpMaxDistanceBonus", "Jump Distance", "Дальность прыжка", "#8A8494", [new("amount", "+", "number")]),
        new("BlockRegainHP", "Block Healing", "Блокировать исцеление", "#E74C3C", []),
        new("IgnoreFallDamage", "Ignore Fall Damage", "Нет урона от падения", "#8A8494", []),
        new("IgnoreResistance", "Ignore Resistance", "Игнорировать сопротивление", "#E67E22",
            [new("type", "Damage", "enum", DamageTypes), new("level", "Level", "enum", ResistanceLevels)]),
        new("ObjectSize", "Size Change", "Изменение размера", "#8A8494", [new("value", "+/-", "number")]),
        new("CharacterWeaponDamage", "Extra Weapon Damage", "Доп. урон оружием", "#E74C3C", [new("dice", "Dice", "dice")]),
        new("Tag", "Add Tag", "Добавить тег", "#8A8494", [new("tag", "Tag", "string")]),
        new("MinimumRollResult", "Minimum Roll", "Мин. результат броска", "#3498DB",
            [new("type", "Type", "string"), new("min", "Min", "number")]),
        new("Reroll", "Reroll", "Переброс", "#3498DB",
            [new("type", "Type", "string"), new("threshold", "If ≤", "number")]),
    ];

    // ═══════ STATSFUNCTORS ═══════

    public static readonly BlockDef[] Functors =
    [
        new("DealDamage", "Deal Damage", "Нанести урон", "#E74C3C",
            [new("dice", "Dice", "dice"), new("type", "Type", "enum", DamageTypes)]),
        new("RegainHitPoints", "Heal", "Исцеление", "#2ECC71", [new("amount", "Amount", "formula")]),
        new("ApplyStatus", "Apply Status", "Наложить статус", "#E67E22",
            [new("status", "Status", "string"), new("chance", "%", "number"), new("duration", "Turns", "number")]),
        new("RemoveStatus", "Remove Status", "Снять статус", "#F1C40F", [new("status", "Status", "string")]),
        new("RestoreResource", "Restore Resource", "Восстановить ресурс", "#9B59B6",
            [new("type", "Resource", "enum", ActionResources), new("amount", "Amount", "number"), new("level", "Level", "number")]),
        new("CreateSurface", "Create Surface", "Создать поверхность", "#3498DB",
            [new("radius", "Radius", "number"), new("duration", "Duration", "number"), new("type", "Type", "enum", SurfaceTypes)]),
        new("CreateExplosion", "Create Explosion", "Создать взрыв", "#E74C3C", [new("projectile", "Projectile", "string")]),
        new("Knockback", "Knockback", "Отброс", "#E67E22", [new("distance", "Distance", "number")]),
        new("SavingThrow", "Saving Throw", "Спасбросок", "#F1C40F",
            [new("ability", "Ability", "enum", Abilities), new("dc", "DC", "formula")]),
        new("Teleport", "Teleport", "Телепортация", "#9B59B6", []),
        new("Summon", "Summon", "Призвать", "#9B59B6", [new("template", "Template", "string")]),
    ];

    // ═══════ SCOPE MODIFIERS ═══════

    public static readonly Dictionary<string, string> Scopes = new()
    {
        ["SELF"] = "on Self / на себя",
        ["SWAP"] = "on Attacker / на атакующего",
    };

    // ═══════ TRIGGER EVENTS ═══════

    public static readonly Dictionary<string, string> TriggerEvents = new()
    {
        ["OnDamage"] = "When dealing damage / При нанесении урона",
        ["OnDamaged"] = "When taking damage / При получении урона",
        ["OnCast"] = "When casting / При касте",
        ["OnCastResolved"] = "After cast / После каста",
        ["OnTurn"] = "Each turn / Каждый ход",
        ["OnAttack"] = "When attacking / При атаке",
        ["OnAttacked"] = "When attacked / При атаке на вас",
        ["OnStatusApply"] = "When applying status / При наложении статуса",
        ["OnStatusApplied"] = "When status applied / Когда наложен статус",
        ["OnHeal"] = "When healing / При исцелении",
        ["OnHealed"] = "When healed / Когда исцелён",
        ["OnCombatStarted"] = "Combat starts / Начало боя",
        ["OnCombatEnded"] = "Combat ends / Конец боя",
        ["OnShortRest"] = "Short rest / Короткий отдых",
        ["OnLongRest"] = "Long rest / Длинный отдых",
        ["OnEquip"] = "When equipped / При экипировке",
    };

    // ═══════ CONDITIONS ═══════

    public static readonly Dictionary<string, string> Conditions = new()
    {
        ["Enemy()"] = "Is enemy / Враг",
        ["Ally()"] = "Is ally / Союзник",
        ["Self()"] = "Is self / Это я",
        ["Combat()"] = "In combat / В бою",
        ["IsMeleeAttack()"] = "Melee attack / Ближняя атака",
        ["IsRangedWeaponAttack()"] = "Ranged attack / Дальняя атака",
        ["IsSpellAttack()"] = "Spell attack / Атака заклинанием",
        ["IsWeaponAttack()"] = "Weapon attack / Атака оружием",
        ["IsCritical()"] = "Critical hit / Крит. удар",
        ["IsMiss()"] = "Miss / Промах",
        ["IsSpell()"] = "Is spell / Заклинание",
        ["HasShieldEquipped()"] = "Has shield / Есть щит",
        ["not Dead()"] = "Not dead / Не мёртв",
    };

    // ═══════ ENUMS ═══════

    public static readonly string[] DamageTypes =
        ["Slashing", "Piercing", "Bludgeoning", "Acid", "Thunder", "Necrotic", "Fire", "Lightning", "Cold", "Psychic", "Poison", "Radiant", "Force"];

    public static readonly string[] Abilities =
        ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];

    public static readonly string[] WeaponProperties =
        ["Finesse", "Light", "Heavy", "Melee", "Magical", "Reach", "Thrown", "Twohanded", "Versatile", "Loading", "Ammunition", "Dippable"];

    public static readonly string[] ArmorTypes =
        ["None", "Cloth", "Padded", "Leather", "StuddedLeather", "Hide", "ChainShirt", "ScaleMail", "BreastPlate", "HalfPlate", "RingMail", "ChainMail", "Splint", "Plate"];

    public static readonly string[] Skills =
        ["Acrobatics", "AnimalHandling", "Arcana", "Athletics", "Deception", "History", "Insight", "Intimidation", "Investigation", "Medicine", "Perception", "Performance", "Persuasion", "Religion", "SleightOfHand", "Stealth", "Survival"];

    public static readonly string[] ResistanceLevels =
        ["Resistant", "Immune", "Vulnerable"];

    public static readonly string[] AttackTypes =
        ["Attack", "MeleeWeaponAttack", "RangedWeaponAttack", "MeleeSpellAttack", "RangedSpellAttack"];

    public static readonly string[] AdvantageTypes =
        ["Attack", "AttackRoll", "SavingThrow", "AllSavingThrows", "Concentration", "DeathSavingThrow"];

    public static readonly string[] ProficiencyTypes =
        ["LightArmor", "MediumArmor", "HeavyArmor", "Shields", "SimpleMeleeWeapon", "SimpleRangedWeapon", "MartialMeleeWeapon", "MartialRangedWeapon"];

    public static readonly string[] ActionResources =
        ["ActionPoint", "BonusActionPoint", "Movement", "SpellSlot", "KiPoint", "Rage", "SorceryPoint", "BardicInspiration", "SuperiorityDie", "ChannelDivinity", "LayOnHandsCharge", "WildShape", "NaturalRecovery"];

    public static readonly string[] SurfaceTypes =
        ["Water", "WaterElectrified", "WaterFrozen", "Blood", "BloodElectrified", "BloodFrozen", "Poison", "Oil", "Lava", "Grease", "Web", "Fire", "Acid"];

    // ═══════ PARSING ═══════

    /// <summary>
    /// Parse a raw boost string like "AC(2)" into (funcName, args[]).
    /// Handles nested parentheses: IF(Enemy()):DealDamage(1d6,Fire) → funcName="IF", args=["Enemy():DealDamage(1d6,Fire)"]
    /// </summary>
    public static (string funcName, string[] args)? ParseBoostCall(string raw)
    {
        raw = raw.Trim();

        // Handle IF(...):Effect pattern
        if (raw.StartsWith("IF", StringComparison.OrdinalIgnoreCase) && raw.Contains(':'))
        {
            return ("IF", [raw[2..].Trim()]);
        }

        var parenIdx = raw.IndexOf('(');
        if (parenIdx < 0)
            return (raw, []); // No args, e.g. "BlockRegainHP"

        var funcName = raw[..parenIdx];

        // Find matching close paren (handle nesting)
        int depth = 0;
        int closeIdx = -1;
        for (int i = parenIdx; i < raw.Length; i++)
        {
            if (raw[i] == '(') depth++;
            else if (raw[i] == ')') { depth--; if (depth == 0) { closeIdx = i; break; } }
        }

        if (closeIdx < 0) closeIdx = raw.Length - 1;
        var argsStr = raw[(parenIdx + 1)..closeIdx];

        // Split args by comma, but respect nested parens
        var args = SplitArgs(argsStr);
        return (funcName, args);
    }

    private static string[] SplitArgs(string argsStr)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < argsStr.Length; i++)
        {
            if (argsStr[i] == '(') depth++;
            else if (argsStr[i] == ')') depth--;
            else if (argsStr[i] == ',' && depth == 0)
            {
                result.Add(argsStr[start..i].Trim());
                start = i + 1;
            }
        }
        if (start < argsStr.Length)
            result.Add(argsStr[start..].Trim());
        return result.Where(a => a.Length > 0).ToArray();
    }

    /// <summary>Human-readable label for an armor type.</summary>
    public static string ArmorTypeLabel(string type) => type switch
    {
        "None" => "None / Нет",
        "Cloth" => "Cloth / Ткань",
        "Padded" => "Padded / Стёганая",
        "Leather" => "Leather / Кожаная",
        "StuddedLeather" => "Studded Leather / Проклёпанная кожа",
        "Hide" => "Hide / Шкурная",
        "ChainShirt" => "Chain Shirt / Кольчужная рубаха",
        "ScaleMail" => "Scale Mail / Чешуйчатая",
        "BreastPlate" => "Breastplate / Нагрудник",
        "HalfPlate" => "Half Plate / Полулаты",
        "RingMail" => "Ring Mail / Кольчуга",
        "ChainMail" => "Chain Mail / Кольчужная",
        "Splint" => "Splint / Шинная",
        "Plate" => "Plate / Латная",
        _ => type
    };

    /// <summary>Human-readable label for proficiency.</summary>
    public static string ProficiencyLabel(string prof) => prof switch
    {
        "LightArmor" => "Light Armor / Лёгкая броня",
        "MediumArmor" => "Medium Armor / Средняя броня",
        "HeavyArmor" => "Heavy Armor / Тяжёлая броня",
        "Shields" => "Shields / Щиты",
        "SimpleMeleeWeapon" => "Simple Melee / Простое ближнее",
        "SimpleRangedWeapon" => "Simple Ranged / Простое дальнобойное",
        "MartialMeleeWeapon" => "Martial Melee / Воинское ближнее",
        "MartialRangedWeapon" => "Martial Ranged / Воинское дальнобойное",
        _ => prof
    };

    /// <summary>Human-readable labels for armor types (for ComboBox).</summary>
    public static readonly string[] ArmorTypeLabels = ArmorTypes.Select(ArmorTypeLabel).ToArray();

    /// <summary>Human-readable labels for proficiency (for ComboBox).</summary>
    public static readonly string[] ProficiencyLabels = ProficiencyTypes.Select(ProficiencyLabel).ToArray();

    /// <summary>Human-readable label for a weapon property.</summary>
    public static string WeaponPropertyLabel(string prop) => prop switch
    {
        "Finesse" => "Finesse / Фехтовальное",
        "Light" => "Light / Лёгкое",
        "Heavy" => "Heavy / Тяжёлое",
        "Melee" => "Melee / Ближний бой",
        "Magical" => "Magical / Магическое",
        "Reach" => "Reach / Досягаемость",
        "Thrown" => "Thrown / Метательное",
        "Twohanded" => "Two-handed / Двуручное",
        "Versatile" => "Versatile / Универсальное",
        _ => prop
    };

    /// <summary>
    /// Find block definition by function name.
    /// </summary>
    public static BlockDef? FindBoost(string funcName) =>
        Boosts.FirstOrDefault(b => b.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase));

    public static BlockDef? FindFunctor(string funcName) =>
        Functors.FirstOrDefault(f => f.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase));
}
