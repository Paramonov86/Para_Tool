namespace ParaTool.Core.Schema;

/// <summary>
/// Complete mapping of BG3 Boosts, StatsFunctors and their arguments.
/// Source: LSLibDefinitions.xml + ValueLists.txt + BG3_Comprehensive_Boosts_Syntax_Reference.md
/// </summary>
public static class BoostMapping
{
    public record BlockDef(string FuncName, string Label, string LabelRu, string Color, ParamDef[] Params);
    public record ParamDef(string Name, string Label, string Type, string[]? EnumValues = null);

    // ═══════════════════════════════════════════════════════════
    // ENUM VALUE LISTS (must be BEFORE Boosts/Functors — static init order!)
    // ═══════════════════════════════════════════════════════════

    public static readonly string[] Abilities = ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];
    public static readonly string[] DamageTypes = ["None", "Slashing", "Piercing", "Bludgeoning", "Acid", "Thunder", "Necrotic", "Fire", "Lightning", "Cold", "Psychic", "Poison", "Radiant", "Force"];
    public static readonly string[] AllOrDamageType = ["All", ..DamageTypes];
    public static readonly string[] DamageTypesExtended = [..DamageTypes[1..], "MainWeaponDamageType", "OffhandWeaponDamageType", "MainMeleeWeaponDamageType", "OffhandMeleeWeaponDamageType", "MainRangedWeaponDamageType", "OffhandRangedWeaponDamageType", "SourceWeaponDamageType", "ThrownWeaponDamageType"];
    public static readonly string[] StatsRollType = ["Attack", "MeleeWeaponAttack", "RangedWeaponAttack", "MeleeSpellAttack", "RangedSpellAttack", "MeleeUnarmedAttack", "RangedUnarmedAttack", "MeleeOffHandWeaponAttack", "RangedOffHandWeaponAttack", "SkillCheck", "SavingThrow", "RawAbility", "Damage", "DeathSavingThrow", "MeleeWeaponDamage", "RangedWeaponDamage", "MeleeSpellDamage", "RangedSpellDamage", "MeleeUnarmedDamage", "RangedUnarmedDamage"];
    public static readonly string[] AdvantageContext = ["AttackRoll", "AttackTarget", "SavingThrow", "AllSavingThrows", "Ability", "AllAbilities", "Skill", "AllSkills", "SourceDialogue", "DeathSavingThrow", "Concentration"];
    public static readonly string[] SkillType = ["Deception", "Intimidation", "Performance", "Persuasion", "Acrobatics", "SleightOfHand", "Stealth", "Arcana", "History", "Investigation", "Nature", "Religion", "Athletics", "AnimalHandling", "Insight", "Medicine", "Perception", "Survival"];
    public static readonly string[] AbilityOrSkill = [..Abilities, ..SkillType, ..StatsRollType];
    public static readonly string[] ResistanceBoostFlags = ["None", "Resistant", "Immune", "Vulnerable", "BelowDamageThreshold", "ResistantToMagical", "ImmuneToMagical", "VulnerableToMagical", "ResistantToNonMagical", "ImmuneToNonMagical", "VulnerableToNonMagical"];
    public static readonly string[] ProficiencyBonusBoostType = ["AttackRoll", "AttackTarget", "SavingThrow", "AllSavingThrows", "Ability", "AllAbilities", "Skill", "AllSkills", "SourceDialogue", "WeaponActionDC"];
    public static readonly string[] CriticalHitType = ["AttackTarget", "AttackRoll"];
    public static readonly string[] CriticalHitResult = ["Success", "Failure"];
    public static readonly string[] CriticalHitWhen = ["Never", "Always", "ForcedAlways"];
    public static readonly string[] AttackType = ["DirectHit", "MeleeWeaponAttack", "RangedWeaponAttack", "MeleeOffHandWeaponAttack", "RangedOffHandWeaponAttack", "MeleeSpellAttack", "RangedSpellAttack", "MeleeUnarmedAttack", "RangedUnarmedAttack"];
    public static readonly string[] DamageReductionType = ["Half", "Flat", "Threshold"];
    public static readonly string[] WeaponFlags = ["None", "Light", "Ammunition", "Finesse", "Heavy", "Loading", "Range", "Reach", "Lance", "Net", "Thrown", "Twohanded", "Versatile", "Melee", "Dippable", "Torch", "NoDualWield", "Magical", "NeedDualWieldingBoost", "NotSheathable", "Unstowable", "AddToHotbar"];
    public static readonly string[] ArmorTypes = ["None", "Cloth", "Padded", "Leather", "StuddedLeather", "Hide", "ChainShirt", "ScaleMail", "BreastPlate", "HalfPlate", "RingMail", "ChainMail", "Splint", "Plate"];
    public static readonly string[] SpellSchool = ["None", "Abjuration", "Conjuration", "Divination", "Enchantment", "Evocation", "Illusion", "Necromancy", "Transmutation"];
    public static readonly string[] SpellCooldownType = ["Default", "OncePerTurn", "OncePerCombat", "UntilRest", "OncePerTurnNoRealtime", "UntilShortRest", "UntilPerRestPerItem", "OncePerShortRestPerItem"];
    public static readonly string[] UnlockSpellType = ["Singular", "AddChildren", "MostPowerful"];
    public static readonly string[] HealingDirection = ["Incoming", "Outgoing"];
    public static readonly string[] MovementSpeedType = ["Stroll", "Walk", "Run", "Sprint"];
    public static readonly string[] SurfaceTypes = ["None", "Water", "WaterElectrified", "WaterFrozen", "Blood", "BloodElectrified", "BloodFrozen", "Poison", "Oil", "Lava", "Grease", "Web", "Deepwater", "Vines", "Fire", "Acid", "Mud", "Alcohol"];
    public static readonly string[] SurfaceChange = ["None", "Ignite", "Douse", "Electrify", "Deelectrify", "Freeze", "Melt", "Vaporize", "Condense", "DestroyWater", "Clear"];
    public static readonly string[] ZoneShape = ["Cone", "Square"];
    public static readonly string[] ForceFunctorOrigin = ["OriginToEntity", "OriginToTarget", "TargetToEntity"];
    public static readonly string[] ForceFunctorAggression = ["Aggressive", "Friendly", "Neutral"];
    public static readonly string[] ExecuteWeaponFunctorsType = ["MainHand", "OffHand", "BothHands"];
    public static readonly string[] StatItemSlot = ["Helmet", "Breast", "Cloak", "MeleeMainHand", "MeleeOffHand", "RangedMainHand", "RangedOffHand", "Ring", "Underwear", "Boots", "Gloves", "Amulet", "Ring2", "Wings", "Horns", "Overhead", "MusicalInstrument", "VanityBody", "VanityBoots", "MainHand", "OffHand"];
    public static readonly string[] SetStatusDurationType = ["SetMinimum", "ForceSet", "Add", "Multiply"];
    public static readonly string[] RollAdjustmentType = ["All", "Distribute"];
    public static readonly string[] AttributeFlags = ["None", "SlippingImmunity", "Torch", "Arrow", "Unbreakable", "Grounded", "Floating", "InventoryBound", "IgnoreClouds", "BackstabImmunity", "ThrownImmunity", "InvisibilityImmunity"];
    public static readonly string[] ProficiencyGroupFlags = ["LightArmor", "MediumArmor", "HeavyArmor", "Shields", "SimpleMeleeWeapon", "SimpleRangedWeapon", "MartialMeleeWeapon", "MartialRangedWeapon", "HandCrossbows", "Battleaxes", "Flails", "Glaives", "Greataxes", "Greatswords", "Halberds", "Longswords", "Mauls", "Morningstars", "Pikes", "Rapiers", "Scimitars", "Shortswords", "Tridents", "WarPicks", "Warhammers", "Clubs", "Daggers", "Greatclubs", "Handaxes", "Javelins", "LightHammers", "Maces", "Quarterstaffs", "Sickles", "Spears", "LightCrossbows", "Darts", "Shortbows", "Slings", "Longbows", "HeavyCrossbows", "MusicalInstrument"];
    public static readonly string[] SurfaceLayers = ["Ground", "Cloud"];
    public static readonly string[] DeathTypes = ["None", "Acid", "Chasm", "DoT", "Electrocution", "Explode", "Falling", "Incinerate", "KnockedDown", "Lifetime", "Narcolepsy", "PetrifiedShattered", "Sentinel"];
    public static readonly string[] ResurrectTypes = ["Living", "Guaranteed", "Construct", "Undead"];
    public static readonly string[] SummonDurations = ["UntilLongRest", "Permanent"];
    public static readonly string[] MagicalFlags = ["Magical", "Nonmagical"];
    public static readonly string[] NonlethalFlags = ["Lethal", "Nonlethal"];
    public static readonly string[] SizeCategories = ["Tiny", "Small", "Medium", "Large", "Huge", "Gargantuan"];
    public static readonly string[] EngineStatusTypes = ["DYING", "HEAL", "KNOCKED_DOWN", "TELEPORT_FALLING", "BOOST", "REACTION", "STORY_FROZEN", "SNEAKING", "UNLOCK", "FEAR", "SMELLY", "INVISIBLE", "ROTATE", "MATERIAL", "CLIMBING", "INCAPACITATED", "INSURFACE", "POLYMORPHED", "EFFECT", "DEACTIVATED", "DOWNED"];
    public static readonly string[] StatusRemoveCause = ["Condition", "TimeOut", "Death"];
    public static readonly string[] ObscuredState = ["Clear", "BabyBent", "BentQuarters", "ThreeQuarters", "FullCover"];
    public static readonly string[] WeaponProperties = WeaponFlags;
    public static readonly string[] ProficiencyTypes = ["", "LightArmor", "MediumArmor", "HeavyArmor", "Shields", "SimpleMeleeWeapon", "SimpleRangedWeapon", "MartialMeleeWeapon", "MartialRangedWeapon"];
    public static readonly string[] Skills = SkillType;
    public static readonly string[] ActionResources = ["ActionPoint", "BonusActionPoint", "Movement", "SpellSlot", "KiPoint", "Rage", "SorceryPoint", "BardicInspiration", "SuperiorityDie", "ChannelDivinity", "LayOnHandsCharge", "WildShape", "NaturalRecovery"];

    // ═══════════════════════════════════════════════════════════
    // BOOSTS — Full list from LSLibDefinitions.xml
    // ═══════════════════════════════════════════════════════════

    public static readonly BlockDef[] Boosts =
    [
        // ── Ability & Stats (#2ECC71) ──
        new("AC", "Armor Class", "Класс брони", "#2ECC71", [new("AC", "+/-", "number")]),
        new("Ability", "Ability Score", "Показатель способности", "#2ECC71",
            [new("Ability", "Ability", "enum", Abilities), new("Amount", "+/-", "number"), new("Cap", "Cap", "optnum"), new("Savant", "Savant", "optbool")]),
        new("AbilityOverrideMinimum", "Min Ability", "Минимум способности", "#2ECC71",
            [new("Ability", "Ability", "enum", Abilities), new("Min", "Min", "number"), new("Savant", "Savant", "optbool")]),
        new("NullifyAbilityScore", "Nullify Ability", "Обнулить способность", "#2ECC71",
            [new("Ability", "Ability", "enum", Abilities)]),
        new("ACOverrideFormula", "AC Override Formula", "Формула КБ", "#2ECC71",
            [new("AC", "Base AC", "number"), new("AddAbilityMods", "Add Mods", "bool"), new("Ability1", "Ability 1", "enum", Abilities), new("Ability2", "Ability 2", "enum", Abilities)]),
        new("AddProficiencyToAC", "Prof to AC", "Умение к КБ", "#2ECC71", []),
        new("AddProficiencyToDamage", "Prof to Damage", "Умение к урону", "#2ECC71", []),
        new("BlockAbilityModifierFromAC", "Block Ability Mod from AC", "Блокировать мод. от КБ", "#2ECC71",
            [new("Ability", "Ability", "enum", Abilities)]),
        new("ProficiencyBonusOverride", "Prof Bonus Override", "Переопредел. бонуса умения", "#2ECC71", [new("Bonus", "Bonus", "formula")]),
        new("ProficiencyBonusIncrease", "Prof Bonus Increase", "Увеличить бонус умения", "#2ECC71", [new("Amount", "+", "number")]),
        new("HalveWeaponDamage", "Halve Weapon Damage", "Половинный урон оружием", "#2ECC71", [new("Ability", "Ability", "enum", Abilities)]),

        // ── Attack & Damage (#E06040) ──
        new("RollBonus", "Roll Bonus", "Бонус к броску", "#E06040",
            [new("RollType", "Type", "enum", StatsRollType), new("Bonus", "Bonus", "formula"), new("AbilityOrSkill", "Ability/Skill", "enum", AbilityOrSkill)]),
        new("DamageBonus", "Damage Bonus", "Бонус к урону", "#E06040",
            [new("Amount", "Amount", "formula"), new("DamageType", "Type", "enum", DamageTypes)]),
        new("CharacterWeaponDamage", "Extra Weapon Damage", "Доп. урон оружием", "#E06040",
            [new("Amount", "Amount", "formula"), new("DamageType", "Type", "enum", DamageTypes)]),
        new("CharacterUnarmedDamage", "Unarmed Damage", "Урон без оружия", "#E06040",
            [new("Damage", "Damage", "formula"), new("DamageType", "Type", "enum", DamageTypes)]),
        new("WeaponDamage", "Weapon Damage", "Урон оружия", "#E06040",
            [new("Amount", "Amount", "formula"), new("DamageType", "Type", "enum", DamageTypes)]),
        new("WeaponEnchantment", "Weapon Enchantment", "Зачарование оружия", "#E06040", [new("Level", "+", "number")]),
        new("WeaponAttackRollBonus", "Weapon Attack Bonus", "Бонус атаки оружием", "#E06040", [new("Amount", "Bonus", "formula")]),
        new("WeaponProperty", "Weapon Property", "Свойство оружия", "#E06040", [new("Flags", "Property", "enum", WeaponFlags)]),
        new("WeaponAttackTypeOverride", "Attack Type Override", "Тип атаки", "#E06040", [new("Type", "Type", "enum", AttackType)]),
        new("WeaponDamageDieOverride", "Damage Die Override", "Кубик урона", "#E06040", [new("Die", "Die", "dice")]),
        new("WeaponDamageTypeOverride", "Damage Type Override", "Тип урона", "#E06040", [new("Type", "Type", "enum", DamageTypes)]),
        new("WeaponAttackRollAbilityOverride", "Attack Ability Override", "Способн. атаки", "#E06040", [new("Ability", "Ability", "enum", Abilities)]),
        new("WeaponDamageResistance", "Weapon Dmg Resistance", "Сопр. урону оружия", "#E06040",
            [new("Type1", "Type 1", "enum", DamageTypes), new("Type2", "Type 2", "enum", DamageTypes)]),
        new("EntityThrowDamage", "Throw Damage", "Урон от метания", "#E06040", [new("Die", "Die", "dice"), new("Type", "Type", "enum", DamageTypes)]),
        new("DamageReduction", "Damage Reduction", "Снижение урона", "#E06040",
            [new("DmgType", "Damage", "enum", AllOrDamageType), new("Method", "Method", "enum", DamageReductionType), new("Amount", "Amount", "formula")]),
        new("DamageTakenBonus", "Damage Taken Bonus", "Бонус получаем. урона", "#E06040",
            [new("Amount", "Amount", "formula"), new("Type", "Type", "enum", DamageTypes)]),

        // ── Critical Hits (#E74C3C) ──
        new("CriticalHit", "Critical Hit", "Критический удар", "#E06040",
            [new("Type", "Type", "enum", CriticalHitType), new("Result", "Result", "enum", CriticalHitResult), new("When", "When", "enum", CriticalHitWhen)]),
        new("CriticalHitExtraDice", "Extra Crit Dice", "Доп. кубики крита", "#E06040",
            [new("Dice", "Dice", "number"), new("AttackType", "Attack", "enum", AttackType)]),
        new("ReduceCriticalAttackThreshold", "Lower Crit Threshold", "Снизить порог крита", "#E06040",
            [new("Threshold", "By", "number"), new("StatusId", "Status", "string")]),
        new("CriticalDamageOnHit", "Crit Damage on Hit", "Крит. урон при попадании", "#E06040", []),

        // ── Resistance & Immunity (#F1C40F) ──
        new("Resistance", "Resistance", "Устойчивость", "#F1C40F",
            [new("DmgType", "Damage", "enum", AllOrDamageType), new("Flags", "Level", "enum", ResistanceBoostFlags)]),
        new("StatusImmunity", "Status Immunity", "Невосприимч. к статусу", "#F1C40F", [new("StatusId", "Status", "string")]),
        new("IgnoreResistance", "Ignore Resistance", "Игнор. устойчивость", "#F1C40F",
            [new("Type", "Damage", "enum", DamageTypes), new("Flags", "Level", "enum", ResistanceBoostFlags)]),
        new("IgnoreDamageThreshold", "Ignore Dmg Threshold", "Игнор. порог урона", "#F1C40F",
            [new("Type", "Damage", "enum", AllOrDamageType), new("Threshold", "Threshold", "number")]),
        new("SpellResistance", "Spell Resistance", "Устойч. заклинаниям", "#F1C40F", [new("Level", "Level", "enum", ResistanceBoostFlags)]),
        new("Invulnerable", "Invulnerable", "Неуязвимость", "#F1C40F", []),
        new("RedirectDamage", "Redirect Damage", "Перенаправить урон", "#F1C40F",
            [new("Mult", "Multiplier", "float"), new("TypeOut", "Out Type", "enum", DamageTypes), new("TypeIn", "In Type", "enum", DamageTypes), new("Redirect", "To Source", "bool")]),

        // ── Advantage & Rolls (#3498DB) ──
        new("Advantage", "Advantage", "Преимущество", "#3498DB",
            [new("Type", "On", "enum", AdvantageContext), new("Arg2", "Ability/Skill", "enum", AbilityOrSkill)]),
        new("Disadvantage", "Disadvantage", "Помеха", "#3498DB",
            [new("Type", "On", "enum", AdvantageContext), new("Arg2", "Ability/Skill", "enum", AbilityOrSkill)]),
        new("Reroll", "Reroll", "Переброс", "#3498DB",
            [new("Type", "Type", "enum", StatsRollType), new("Below", "If ≤", "number"), new("Always", "Always", "bool")]),
        new("MinimumRollResult", "Minimum Roll", "Мин. результат", "#3498DB",
            [new("Type", "Type", "enum", StatsRollType), new("Min", "Min", "number")]),
        new("MaximumRollResult", "Maximum Roll", "Макс. результат", "#3498DB",
            [new("Type", "Type", "enum", StatsRollType), new("Max", "Max", "number")]),
        new("GuaranteedChanceRollOutcome", "Guaranteed Outcome", "Гарант. результат", "#3498DB", [new("Success", "Success", "bool")]),

        // ── Proficiency (#3498DB) ──
        new("Proficiency", "Proficiency", "Умение", "#3498DB",
            [new("Group", "Group", "enum", ProficiencyGroupFlags)]),
        new("ProficiencyBonus", "Proficiency Bonus", "Бонус умения", "#3498DB",
            [new("Type", "Type", "enum", ProficiencyBonusBoostType), new("Skill", "Skill/Ability", "enum", AbilityOrSkill)]),
        new("ExpertiseBonus", "Expertise", "Мастерство", "#3498DB", [new("Skill", "Skill", "enum", SkillType)]),
        new("Skill", "Skill Bonus", "Бонус к навыку", "#3498DB",
            [new("Skill", "Skill", "enum", SkillType), new("Amount", "+/-", "formula")]),

        // ── Spells & Magic (#9B59B6) ──
        new("SpellSaveDC", "Spell Save DC", "КС заклинаний", "#9B59B6", [new("DC", "+/-", "number")]),
        new("UnlockSpell", "Unlock Spell", "Открыть заклинание", "#9B59B6",
            [new("SpellId", "Spell", "string"), new("Type", "Type", "enum", UnlockSpellType), new("Guid", "Guid", "string"), new("Cooldown", "Cooldown", "enum", SpellCooldownType), new("Ability", "Ability", "enum", Abilities)]),
        new("UnlockSpellVariant", "Unlock Spell Variant", "Вариант заклинания", "#9B59B6", [new("Mods", "Modifications", "formula")]),
        new("UnlockInterrupt", "Unlock Interrupt", "Открыть ответное", "#9B59B6", [new("Interrupt", "Interrupt", "string")]),
        new("Savant", "Savant", "Знаток школы", "#9B59B6", [new("School", "School", "enum", SpellSchool)]),
        new("BlockSpellCast", "Block Spell Cast", "Блок каста", "#9B59B6", []),
        new("ConcentrationIgnoreDamage", "Concentration Ignore Dmg", "Концентрация игнор. урон", "#9B59B6", [new("School", "School", "enum", SpellSchool)]),
        new("UseBoosts", "Use Boosts", "Применить бусты", "#9B59B6", [new("Boosts", "Boosts", "string")]),

        // ── Resources (#9B59B6) ──
        new("ActionResource", "Action Resource", "Ресурс", "#9B59B6",
            [new("Resource", "Resource", "enum", ActionResources), new("Amount", "Amount", "float"), new("Level", "Level", "number")]),
        new("ActionResourceOverride", "Resource Override", "Переопредел. ресурса", "#9B59B6",
            [new("Resource", "Resource", "enum", ActionResources), new("Amount", "Amount", "float"), new("Level", "Level", "number")]),
        new("ActionResourceMultiplier", "Resource Multiplier", "Множитель ресурса", "#9B59B6",
            [new("Resource", "Resource", "enum", ActionResources), new("Mult", "Multiplier", "number"), new("Level", "Level", "number")]),
        new("ActionResourceBlock", "Block Resource", "Блок ресурса", "#9B59B6", [new("Resource", "Resource", "enum", ActionResources)]),

        // ── HP & Healing (#2ECC71) ──
        new("IncreaseMaxHP", "Increase Max HP", "Увеличить макс. ОЗ", "#2ECC71", [new("Amount", "Amount", "formula")]),
        new("TemporaryHP", "Temporary HP", "Врем. ОЗ", "#2ECC71", [new("Amount", "Amount", "formula")]),
        new("BlockRegainHP", "Block Healing", "Блок исцеления", "#2ECC71", [new("Type", "Type", "enum", ResurrectTypes)]),
        new("MaximizeHealing", "Maximize Healing", "Максим. исцеление", "#2ECC71", [new("Dir", "Direction", "enum", HealingDirection)]),

        // ── Movement & Physical (#8A8494) ──
        new("Initiative", "Initiative", "Инициатива", "#8A8494", [new("Bonus", "+/-", "number")]),
        new("ObjectSize", "Size Change", "Размер", "#8A8494", [new("Size", "+/-", "number")]),
        new("ObjectSizeOverride", "Size Override", "Переопредел. размера", "#8A8494", [new("Size", "Size", "enum", SizeCategories)]),
        new("ScaleMultiplier", "Scale Multiplier", "Множитель масштаба", "#8A8494", [new("Mult", "×", "float")]),
        new("Weight", "Weight", "Вес", "#8A8494", [new("Weight", "kg", "float")]),
        new("CarryCapacityMultiplier", "Carry Capacity", "Грузоподъёмность ×", "#8A8494", [new("Mult", "×", "float")]),
        new("JumpMaxDistanceBonus", "Jump Distance", "Бонус прыжка", "#8A8494", [new("Bonus", "+", "float")]),
        new("JumpMaxDistanceMultiplier", "Jump Distance ×", "Множитель прыжка", "#8A8494", [new("Mult", "×", "float")]),
        new("FallDamageMultiplier", "Fall Damage ×", "Множитель урона падения", "#8A8494", [new("Mult", "×", "float")]),
        new("IgnoreFallDamage", "Ignore Fall Damage", "Нет урона от падения", "#8A8494", []),
        new("IgnoreLeaveAttackRange", "Ignore Leave Range", "Игнор. выход из зоны", "#8A8494", []),
        new("IgnorePointBlankDisadvantage", "Ignore Point Blank", "Игнор. помехи вблизи", "#8A8494", [new("Flags", "Weapon", "enum", WeaponFlags)]),
        new("IgnoreLowGroundPenalty", "Ignore Low Ground", "Игнор. штрафа низины", "#8A8494", [new("Type", "Roll", "enum", StatsRollType)]),
        new("MovementSpeedLimit", "Speed Limit", "Лимит скорости", "#8A8494", [new("Type", "Type", "enum", MovementSpeedType)]),
        new("NonLethal", "Non-Lethal", "Несмертельный", "#8A8494", []),
        new("NoAOEDamageOnLand", "No AOE on Land", "Нет АОЕ приземл.", "#8A8494", []),

        // ── Vision & Light (#8A8494) ──
        new("DarkvisionRange", "Darkvision", "Ночное зрение", "#8A8494", [new("Range", "Range", "float")]),
        new("DarkvisionRangeMin", "Darkvision Min", "Мин. ночного зрения", "#8A8494", [new("Range", "Range", "float")]),
        new("DarkvisionRangeOverride", "Darkvision Override", "Переопредел. ночн. зрения", "#8A8494", [new("Range", "Range", "float")]),
        new("SightRangeAdditive", "Sight Range +", "Доп. обзор", "#8A8494", [new("Range", "+", "float")]),
        new("SightRangeMinimum", "Sight Range Min", "Мин. обзор", "#8A8494", [new("Range", "Min", "float")]),
        new("SightRangeMaximum", "Sight Range Max", "Макс. обзор", "#8A8494", [new("Range", "Max", "float")]),
        new("Invisibility", "Invisibility", "Невидимость", "#8A8494", []),
        new("GameplayLight", "Gameplay Light", "Свет", "#8A8494", [new("Dist", "Distance", "float"), new("Arg2", "Arg2", "bool")]),
        new("GameplayObscurity", "Obscurity", "Скрытность", "#8A8494", [new("Obscurity", "Level", "float")]),

        // ── Tags & Flags (#8A8494) ──
        new("Tag", "Add Tag", "Добавить тег", "#8A8494", [new("Tag", "Tag", "string")]),
        new("Attribute", "Attribute Flags", "Флаги атрибута", "#8A8494", [new("Flags", "Flags", "flags", AttributeFlags)]),
        new("Lootable", "Lootable", "Можно обыскать", "#8A8494", []),
        new("ItemReturnToOwner", "Item Returns", "Предмет возвращается", "#8A8494", []),
        new("CannotBeDisarmed", "Cannot Disarm", "Нельзя обезоружить", "#8A8494", []),
        new("Detach", "Detach", "Отсоединить", "#8A8494", []),

        // ── Combat Mode (#E67E22) ──
        new("DualWielding", "Dual Wielding", "Двуручье", "#E67E22", [new("DW", "Enabled", "bool")]),
        new("TwoWeaponFighting", "Two-Weapon Fighting", "Бой двумя оружиями", "#E67E22", []),
        new("MonkWeaponDamageDiceOverride", "Monk Damage Die", "Кубик монаха", "#E67E22", [new("Die", "Die", "formula")]),
        new("UnarmedMagicalProperty", "Unarmed Magical", "Магич. безоруж.", "#E67E22", []),
        new("ArmorAbilityModifierCapOverride", "Armor Mod Cap", "Макс. мод. брони", "#E67E22",
            [new("ArmorType", "Armor", "enum", ArmorTypes), new("Cap", "Cap", "number")]),
        new("ProjectileDeflect", "Projectile Deflect", "Отражение снарядов", "#E67E22", []),
        new("AreaDamageEvade", "Area Damage Evade", "Уклонение от АОЕ", "#E67E22", []),
        new("SourceAdvantageOnAttack", "Source Advantage", "Преимущ. источника", "#E67E22", []),
        new("DownedStatus", "Downed Status", "Статус поражения", "#E67E22", [new("StatusId", "Status", "string"), new("Priority", "Priority", "number")]),

        // ── Social / Misc (#8A8494) ──
        new("HiddenDuringCinematic", "Hidden in Cinematic", "Скрыт в катсцене", "#8A8494", []),
        new("DialogueBlock", "Block Dialogue", "Блок диалога", "#8A8494", []),
        new("BlockTravel", "Block Travel", "Блок перемещения", "#8A8494", []),
    ];

    // ═══════════════════════════════════════════════════════════
    // FUNCTORS — Full list from LSLibDefinitions.xml
    // ═══════════════════════════════════════════════════════════

    public static readonly BlockDef[] Functors =
    [
        // ── Damage & Healing ──
        new("DealDamage", "Deal Damage", "Нанести урон", "#E06040",
            [new("Damage", "Damage", "formula"), new("Type", "Type", "enum", DamageTypesExtended)]),
        new("RegainHitPoints", "Heal", "Исцеление", "#2ECC71", [new("HP", "Amount", "formula")]),
        new("GainTemporaryHitPoints", "Gain Temp HP", "Получить врем. ОЗ", "#2ECC71", [new("Amount", "Amount", "formula")]),

        // ── Status Effects ──
        new("ApplyStatus", "Apply Status", "Наложить статус", "#E67E22",
            [new("StatusId", "Status", "string"), new("Chance", "%", "hidden"), new("Duration", "Turns", "int")]),
        new("ApplyEquipmentStatus", "Apply Equip Status", "Статус экипировки", "#E67E22",
            [new("Slot", "Slot", "enum", StatItemSlot), new("StatusId", "Status", "string"), new("Chance", "%", "hidden"), new("Duration", "Turns", "int")]),
        new("RemoveStatus", "Remove Status", "Снять статус", "#F1C40F", [new("StatusId", "Status", "string")]),
        new("RemoveUniqueStatus", "Remove Unique Status", "Снять уник. статус", "#F1C40F", [new("StatusId", "Status", "string")]),
        new("SetStatusDuration", "Set Status Duration", "Длительность статуса", "#E67E22",
            [new("StatusId", "Status", "string"), new("Duration", "Duration", "float"), new("ChangeType", "Change", "enum", SetStatusDurationType)]),

        // ── Resources ──
        new("RestoreResource", "Restore Resource", "Восстановить ресурс", "#9B59B6",
            [new("Resource", "Resource", "enum", ActionResources), new("Amount", "Amount", "formula"), new("Level", "Level", "number")]),
        new("UseActionResource", "Use Resource", "Потратить ресурс", "#9B59B6",
            [new("Resource", "Resource", "enum", ActionResources), new("Amount", "Amount", "string"), new("Level", "Level", "number")]),

        // ── Surface & Zone ──
        new("CreateSurface", "Create Surface", "Создать поверхность", "#3498DB",
            [new("Radius", "Radius", "float"), new("Duration", "Duration", "float"), new("Type", "Type", "enum", SurfaceTypes)]),
        new("CreateConeSurface", "Create Cone Surface", "Конусная поверхн.", "#3498DB",
            [new("Radius", "Radius", "float"), new("Duration", "Duration", "float"), new("Type", "Type", "enum", SurfaceTypes)]),
        new("SurfaceChange", "Surface Change", "Изменить поверхность", "#3498DB",
            [new("Change", "Change", "enum", SurfaceChange)]),
        new("CreateZone", "Create Zone", "Создать зону", "#3498DB",
            [new("Shape", "Shape", "enum", ZoneShape), new("Radius", "Radius", "float"), new("Duration", "Duration", "float")]),

        // ── Movement & Positioning ──
        new("Force", "Force (Push/Pull)", "Сила (толчок/тяга)", "#E67E22",
            [new("Distance", "Distance", "formula"), new("Origin", "Origin", "enum", ForceFunctorOrigin), new("Aggression", "Mode", "enum", ForceFunctorAggression)]),
        new("DoTeleport", "Teleport", "Телепортация", "#9B59B6", [new("Arg1", "Range", "float")]),
        new("TeleportSource", "Teleport Source", "Телепорт источника", "#9B59B6", [new("FindPos", "Find Position", "bool")]),
        new("SwapPlaces", "Swap Places", "Поменяться местами", "#9B59B6", []),

        // ── Spells & Combat ──
        new("UseSpell", "Use Spell", "Применить заклинание", "#9B59B6",
            [new("SpellId", "Spell", "string"), new("IgnoreHasSpell", "Flag", "bool"), new("IgnoreChecks", "Flag", "bool")]),
        new("UseAttack", "Use Attack", "Применить атаку", "#E06040", []),
        new("ExecuteWeaponFunctors", "Execute Weapon Functors", "Функторы оружия", "#E06040",
            [new("Type", "Hand", "enum", ExecuteWeaponFunctorsType)]),
        new("Counterspell", "Counterspell", "Контрзаклинание", "#9B59B6", []),
        new("BreakConcentration", "Break Concentration", "Прервать концентрацию", "#9B59B6", []),
        new("ResetCooldowns", "Reset Cooldowns", "Сброс перезарядок", "#9B59B6", [new("Type", "Type", "enum", SpellCooldownType)]),

        // ── Summon & Spawn ──
        new("Summon", "Summon", "Призвать", "#9B59B6",
            [new("Template", "Template", "guid"), new("Duration", "Duration", "string")]),
        new("SummonInInventory", "Summon in Inventory", "Призвать в инвентарь", "#9B59B6",
            [new("Template", "Template", "guid"), new("Duration", "Duration", "string"), new("Amount", "Amount", "number")]),
        new("Unsummon", "Unsummon", "Распризвать", "#9B59B6", []),

        // ── Roll Manipulation ──
        new("AdjustRoll", "Adjust Roll", "Корректировать бросок", "#F1C40F",
            [new("Amount", "Amount", "formula"), new("Type", "Type", "enum", RollAdjustmentType), new("DmgType", "Damage", "enum", DamageTypes)]),
        new("SetRoll", "Set Roll", "Установить бросок", "#F1C40F", [new("Roll", "Roll", "number"), new("Type", "Type", "string")]),
        new("SetReroll", "Set Reroll", "Установить переброс", "#F1C40F", [new("Roll", "Roll", "number"), new("KeepNew", "Keep New", "bool")]),
        new("SetAdvantage", "Set Advantage", "Установить преимущество", "#F1C40F", []),
        new("SetDisadvantage", "Set Disadvantage", "Установить помеху", "#F1C40F", []),
        new("MaximizeRoll", "Maximize Roll", "Максимизировать бросок", "#F1C40F", [new("Type", "Damage", "enum", DamageTypes)]),
        new("SetDamageResistance", "Set Damage Resistance", "Уст. устойчив.", "#F1C40F", [new("Type", "Damage", "enum", DamageTypes)]),

        // ── Misc ──
        new("Resurrect", "Resurrect", "Воскресить", "#2ECC71", [new("Chance", "%", "float"), new("HP", "HP%", "float")]),
        new("Stabilize", "Stabilize", "Стабилизировать", "#2ECC71", []),
        new("Kill", "Kill", "Убить", "#E06040", []),
        new("Douse", "Douse", "Потушить", "#3498DB", [new("Radius", "Radius", "float")]),
        new("CreateExplosion", "Create Explosion", "Создать взрыв", "#E06040", [new("SpellId", "Spell", "string")]),
        new("FireProjectile", "Fire Projectile", "Запустить снаряд", "#E06040", [new("Template", "Template", "string")]),
        new("DisarmWeapon", "Disarm Weapon", "Обезоружить", "#E67E22", []),
        new("ShortRest", "Short Rest", "Короткий отдых", "#2ECC71", []),
        new("TriggerRandomCast", "Trigger Random Cast", "Случайный каст", "#9B59B6",
            [new("DC", "DC", "number"), new("Delay", "Delay", "float"), new("Outcome", "Outcome", "string")]),
        new("Knockback", "Knockback", "Отброс", "#E67E22", [new("Distance", "Distance", "number")]),

        // ── Missing from LSLibDefinitions.xml (added) ──
        new("Spawn", "Spawn", "Создать существо", "#9B59B6",
            [new("Template", "Template", "guid"), new("AiHelper", "AI", "string")]),
        new("SpawnInInventory", "Spawn in Inventory", "Создать в инвентаре", "#9B59B6",
            [new("Template", "Template", "guid"), new("Amount", "Amount", "number")]),
        new("RemoveStatusByLevel", "Remove Status by Level", "Снять статус по уровню", "#F1C40F",
            [new("StatusId", "Status", "string"), new("Level", "Level", "number"), new("Ability", "Ability", "enum", Abilities)]),
        new("RemoveAuraByChildStatus", "Remove Aura by Child", "Снять ауру по потомку", "#F1C40F",
            [new("StatusId", "Status", "string")]),
        new("SurfaceClearLayer", "Clear Surface Layer", "Очистить слой поверхн.", "#3498DB",
            [new("Layer1", "Layer 1", "enum", SurfaceLayers), new("Layer2", "Layer 2", "enum", SurfaceLayers)]),
        new("CreateWall", "Create Wall", "Создать стену", "#3498DB", []),
        new("SwitchDeathType", "Switch Death Type", "Тип смерти", "#E06040",
            [new("Type", "Type", "enum", DeathTypes)]),
        new("RegainTemporaryHitPoints", "Regain Temp HP", "Восстановить врем. ОЗ", "#2ECC71",
            [new("Amount", "Amount", "formula")]),
        new("DisarmAndStealWeapon", "Disarm & Steal", "Обезоружить и украсть", "#E67E22", []),
        new("Unlock", "Unlock", "Открыть замок", "#8A8494", []),
        new("Sabotage", "Sabotage", "Саботаж", "#8A8494", [new("Amount", "Amount", "number")]),
        new("Pickup", "Pick Up", "Подобрать", "#8A8494", [new("Effect", "Effect", "string")]),
        new("Drop", "Drop", "Уронить", "#8A8494", [new("Effect", "Effect", "string")]),
        new("ResetCombatTurn", "Reset Turn", "Сбросить ход", "#E67E22", []),
        new("SpawnExtraProjectiles", "Extra Projectiles", "Доп. снаряды", "#E06040",
            [new("SpellId", "Spell", "string")]),
        new("CameraWait", "Camera Wait", "Ждать камеру", "#8A8494", [new("Duration", "Sec", "float")]),
        new("TutorialEvent", "Tutorial Event", "Событие туториала", "#8A8494", [new("Event", "Event", "guid")]),
    ];

    // ═══════════════════════════════════════════════════════════
    // SCOPE MODIFIERS
    // ═══════════════════════════════════════════════════════════

    public static readonly Dictionary<string, string> Scopes = new()
    {
        ["SELF"] = "on Self / на себя",
        ["SWAP"] = "on Attacker / на атакующего",
        ["OBSERVER_SOURCE"] = "on Observer Source / на источник наблюдателя",
        ["GROUND"] = "on Ground / на поверхность",
    };

    // ═══════════════════════════════════════════════════════════
    // FORMULA / DICE VALUES (sorted by average for tumbler)
    // ═══════════════════════════════════════════════════════════

    /// <summary>Numbers 1-100 + dice 1d4..10d20, sorted by average value.</summary>
    public static readonly string[] FormulaValues =
    [
        "1", "2", "1d4", "3", "1d6", "4", "1d8", "2d4", "5", "1d10", "6", "1d12", "2d6", "7", "3d4",
        "8", "2d8", "9", "10", "4d4", "3d6", "1d20", "11", "2d10", "12", "5d4", "13", "2d12", "3d8", "14", "4d6",
        "15", "6d4", "16", "3d10", "17", "5d6", "7d4", "18", "4d8", "19", "3d12", "20", "8d4", "2d20", "21", "6d6",
        "22", "4d10", "5d8", "9d4", "23", "24", "7d6", "10d4", "25", "26", "4d12", "27", "6d8", "5d10",
        "28", "8d6", "29", "30", "31", "3d20", "7d8", "9d6", "32", "5d12", "33", "6d10", "34", "10d6", "35", "36",
        "8d8", "37", "38", "7d10", "39", "6d12", "40", "9d8", "41", "42", "4d20", "43", "44", "8d10", "10d8", "45",
        "7d12", "46", "47", "48", "49", "9d10", "50", "51", "52", "5d20", "8d12", "53", "54", "10d10", "55", "56",
        "57", "58", "9d12", "59", "60", "61", "62", "63", "6d20", "64", "10d12", "65", "66", "67", "68", "69", "70",
        "71", "72", "73", "7d20", "74", "75", "76", "77", "78", "79", "80", "81", "82", "83", "8d20", "84", "85",
        "86", "87", "88", "89", "90", "91", "92", "93", "9d20", "94", "95", "96", "97", "98", "99", "100", "10d20",
        // Variables (sorted roughly by frequency of use in vanilla)
        "ProficiencyBonus", "Level",
        "SpellcastingAbilityModifier",
        "StrengthModifier", "DexterityModifier", "ConstitutionModifier",
        "IntelligenceModifier", "WisdomModifier", "CharismaModifier",
    ];

    // ═══════════════════════════════════════════════════════════
    // TRIGGER EVENTS (StatsFunctorContext)
    // ═══════════════════════════════════════════════════════════

    public static readonly Dictionary<string, string> TriggerEvents = new()
    {
        ["OnTurn"] = "Each turn / Каждый ход",
        ["OnRound"] = "Each round / Каждый раунд",
        ["OnAttack"] = "On attack / При атаке",
        ["OnAttacked"] = "On attacked / При атаке на вас",
        ["OnDamage"] = "On damage dealt / При нанесении урона",
        ["OnDamaged"] = "On damage taken / При получении урона",
        ["OnHeal"] = "On heal cast / При касте исцеления",
        ["OnHealed"] = "On healed / При получении исцеления",
        ["OnCast"] = "On spell cast / При касте",
        ["OnCastResolved"] = "On cast resolved / Каст завершён",
        ["OnEquip"] = "On equip / При экипировке",
        ["OnCreate"] = "On create / При создании",
        ["OnApply"] = "On apply / При наложении",
        ["OnRemove"] = "On remove / При снятии",
        ["OnApplyAndTurn"] = "On apply + turn / При наложении и ходе",
        ["OnStatusApplied"] = "Status applied / Статус наложен",
        ["OnStatusRemoved"] = "Status removed / Статус снят",
        ["OnStatusApply"] = "Status apply / Наложение статуса",
        ["OnStatusRemove"] = "Status remove / Снятие статуса",
        ["OnSurfaceEnter"] = "Surface enter / Вход на поверхность",
        ["OnObscurityChanged"] = "Visibility change / Смена видимости",
        ["OnCombatStarted"] = "Combat started / Начало боя",
        ["OnCombatEnded"] = "Combat ended / Конец боя",
        ["OnShortRest"] = "Short rest / Короткий отдых",
        ["OnLongRest"] = "Long rest / Длинный отдых",
        ["OnMovedDistance"] = "On moved / При перемещении",
        ["OnPush"] = "On push / При толчке",
        ["OnPushed"] = "On pushed / При толчке на вас",
        ["OnProjectileExploded"] = "Projectile exploded / Снаряд взорвался",
        ["OnActionResourcesChanged"] = "Resources changed / Ресурсы изменены",
        ["OnSpellCast"] = "On spell cast / При касте заклинания",
        ["OnSourceDeath"] = "Source death / Смерть источника",
        ["OnLockpickingSucceeded"] = "Lockpick success / Успех взлома",
        ["OnFactionChanged"] = "Faction changed / Смена фракции",
        ["OnEntityPickUp"] = "Pick up / Подобрать",
        ["OnEntityDrop"] = "Drop / Уронить",
        ["OnUnequip"] = "On unequip / При снятии",
        ["OnMove"] = "On move / При движении",
    };

    // ═══════════════════════════════════════════════════════════
    // CONDITIONS
    // ═══════════════════════════════════════════════════════════

    public static readonly Dictionary<string, string> Conditions = new()
    {
        ["Enemy()"] = "Is Enemy / Враг",
        ["Ally()"] = "Is Ally / Союзник",
        ["Self()"] = "Is Self / Это я",
        ["Combat()"] = "In Combat / В бою",
        ["IsMeleeAttack()"] = "Melee Attack / Ближняя атака",
        ["IsRangedWeaponAttack()"] = "Ranged Attack / Дальняя атака",
        ["IsSpellAttack()"] = "Spell Attack / Атака заклинанием",
        ["IsWeaponAttack()"] = "Weapon Attack / Атака оружием",
        ["IsCritical()"] = "Critical Hit / Крит. удар",
        ["IsMiss()"] = "Miss / Промах",
        ["IsCriticalMiss()"] = "Critical Miss / Крит. промах",
        ["IsSpell()"] = "Is Spell / Заклинание",
        ["IsCantrip()"] = "Is Cantrip / Заговор",
        ["HasShieldEquipped()"] = "Has Shield / Есть щит",
        ["not Dead()"] = "Not Dead / Не мёртв",
        ["HasStatus('X')"] = "Has Status / Есть статус",
        ["SpellId('X')"] = "Is Spell / Это заклинание",
    };

    // ═══════════════════════════════════════════════════════════
    // PARSING
    // ═══════════════════════════════════════════════════════════

    public static (string funcName, string[] args)? ParseBoostCall(string raw)
    {
        raw = raw.Trim();
        if (raw.StartsWith("IF", StringComparison.OrdinalIgnoreCase) && raw.Contains(':'))
            return ("IF", [raw[2..].Trim()]);

        var parenIdx = raw.IndexOf('(');
        if (parenIdx < 0)
            return (raw, []);

        var funcName = raw[..parenIdx];
        int depth = 0, closeIdx = -1;
        for (int i = parenIdx; i < raw.Length; i++)
        {
            if (raw[i] == '(') depth++;
            else if (raw[i] == ')') { depth--; if (depth == 0) { closeIdx = i; break; } }
        }
        if (closeIdx < 0) closeIdx = raw.Length - 1;
        var argsStr = raw[(parenIdx + 1)..closeIdx];
        return (funcName, SplitArgs(argsStr));
    }

    private static string[] SplitArgs(string argsStr)
    {
        var result = new List<string>();
        int depth = 0, start = 0;
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
        return result.ToArray();
    }

    public static BlockDef? FindBoost(string funcName) =>
        Boosts.FirstOrDefault(b => b.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase));

    public static BlockDef? FindFunctor(string funcName) =>
        Functors.FirstOrDefault(f => f.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase));

    // ═══════════════════════════════════════════════════════════
    // ENGINE BOOST DESCRIPTIONS — hardcoded in bg3.exe
    // Maps boost type+args → localized description template
    // [1] is replaced with the first parameter value
    // ═══════════════════════════════════════════════════════════

    public record EngineBoostDesc(string En, string Ru);

    /// <summary>
    /// Boost descriptions extracted from bg3.exe binary.
    /// Key = boost function name (+ optional qualifier), Value = EN/RU templates with [1] placeholder.
    /// </summary>
    public static readonly Dictionary<string, EngineBoostDesc> EngineDescriptions = new()
    {
        // Resistance / Vulnerability / Immunity
        ["Resistance.Resistant"] =          new("Grants Resistance to [1].", "Дает устойчивость: [1]"),
        ["Resistance.Immune"] =             new("Immunity to [1]", "Невосприимчивость к эффекту «[1]»"),
        ["Resistance.Vulnerable"] =         new("Grants Vulnerability to [1].", "Дает уязвимость: [1]"),

        // Armor Class
        ["AC"] =                            new("Armour Class [1]", "Класс брони: [1]"),

        // Weapon Enchantment
        ["WeaponEnchantment"] =             new("Weapon Enchantment", "Зачарование оружия"),

        // Saving Throw Proficiency (per ability)
        ["SavingThrowProf.Strength"] =      new("Proficiency in Strength Saving Throws.", "Умение в испытаниях силы."),
        ["SavingThrowProf.Dexterity"] =     new("Proficiency in Dexterity Saving Throws.", "Умение в испытаниях ловкости."),
        ["SavingThrowProf.Constitution"] =  new("Proficiency in Constitution Saving Throws.", "Умение в испытаниях выносливости."),
        ["SavingThrowProf.Intelligence"] =  new("Proficiency in Intelligence Saving Throws.", "Умение в испытаниях интеллекта."),
        ["SavingThrowProf.Wisdom"] =        new("Proficiency in Wisdom Saving Throws.", "Умение в испытаниях мудрости."),
        ["SavingThrowProf.Charisma"] =      new("Proficiency in Charisma Saving Throws.", "Умение в испытаниях харизмы."),

        // Ability Override Minimum (set ability score)
        ["AbilityOverrideMinimum.Strength"] =     new("Set the wearer's Strength to [1]. The enchantment has no effect if their Strength score is higher without it.", "Увеличивает показатель силы владельца до [1]. Чары не действуют, если показатель силы выше без них."),
        ["AbilityOverrideMinimum.Dexterity"] =    new("Set the wearer's Dexterity score to [1]. The enchantment has no effect if their Dexterity score is higher without it.", "Увеличивает показатель ловкости владельца до [1]. Чары не действуют, если показатель ловкости выше без них."),
        ["AbilityOverrideMinimum.Constitution"] = new("Set the wearer's Constitution score to [1]. The enchantment has no effect if their Constitution score is higher without it.", "Увеличивает показатель выносливости владельца до [1]. Чары не действуют, если показатель выносливости выше без них."),
        ["AbilityOverrideMinimum.Intelligence"] = new("Set the wearer's Intelligence score to [1]. The enchantment has no effect if their Intelligence score is higher without it.", "Увеличивает показатель интеллекта владельца до [1]. Чары не действуют, если показатель интеллекта выше без них."),
        ["AbilityOverrideMinimum.Wisdom"] =       new("Set the wearer's Wisdom to [1]. The enchantment has no effect if their Wisdom score is higher without it.", "Увеличивает показатель мудрости владельца до [1]. Чары не действуют, если показатель мудрости выше без них."),
        ["AbilityOverrideMinimum.Charisma"] =     new("Set the wearer's Charisma score to [1]. The enchantment has no effect if their Charisma score is higher without it.", "Увеличивает показатель харизмы владельца до [1]. Чары не действуют, если показатель харизмы выше без них."),

        // Critical hits
        ["CriticalHit.Success"] =           new("Guaranteed critical hits", "Гарантированы критические удары"),
        ["CriticalHit.NoCrit"] =            new("Attackers can't land Critical Hits on the wearer.", "Защищает владельца от критических ударов."),
        ["CriticalHit.NoCritMiss"] =        new("Protects from critical misses", "Защищает от критических промахов"),
        ["CriticalHit.NoCritHit"] =         new("Your attacks cannot score critical hits.", "Ваши атаки не могут быть критическими."),
        ["CriticalHit.TargetAlwaysCrit"] =  new("All attacks against the wearer are critical hits.", "Все атаки по владельцу становятся критическими."),
        ["CriticalHit.AlwaysCritFail"] =    new("Your attacks always critically fail.", "Ваши атаки всегда проваливаются критически."),

        // Advantage / Disadvantage on checks
        ["Advantage.Ability"] =             new("Advantage on [1] Checks.", "Преимущество при проверках ([1])."),
        ["Disadvantage.Ability"] =          new("Disadvantage on [1] Checks.", "Помеха при проверках ([1])."),
        ["Advantage.AttackRoll"] =          new("Advantage on Attack Rolls.", "Преимущество при броске атаки."),
        ["Disadvantage.AttackRoll"] =       new("Disadvantage on Attack Rolls.", "Помеха при броске атаки."),
        ["Advantage.AttackTarget"] =        new("Attackers have Disadvantage against the wearer.", "Атакующие получают помеху против владельца."),
        ["Disadvantage.AttackTarget"] =     new("Attackers have Advantage against the wearer.", "Атакующие получают преимущество против владельца."),
        ["Advantage.AllSavingThrows"] =     new("Advantage on all Saving Throws.", "Преимущество при всех испытаниях."),
        ["Disadvantage.AllSavingThrows"] =  new("Disadvantage on all Saving Throws.", "Помеха при всех испытаниях."),
        ["Advantage.AllAbilities"] =        new("Advantage on all Ability Checks.", "Преимущество при всех проверках характеристик."),
        ["Disadvantage.AllAbilities"] =     new("Disadvantage on all Ability Checks.", "Помеха при всех проверках характеристик."),
        ["Advantage.AllSkills"] =           new("Advantage on all Skill Checks.", "Преимущество при всех проверках навыков."),
        ["Disadvantage.AllSkills"] =        new("Disadvantage on all Skill Checks.", "Помеха при всех проверках навыков."),
        ["Advantage.Concentration"] =       new("Advantage on Concentration Saving Throws.", "Преимущество при испытаниях концентрации."),
        ["Disadvantage.Concentration"] =    new("Disadvantage on Concentration Saving Throws.", "Помеха при испытаниях концентрации."),
        ["Advantage.DeathSavingThrow"] =    new("Advantage on Death Saving Throws.", "Преимущество при испытаниях от смерти."),
        ["Disadvantage.DeathSavingThrow"] = new("Disadvantage on Death Saving Throws.", "Помеха при испытаниях от смерти."),
        ["Advantage.SavingThrow"] =         new("Advantage on [1] Saving Throws.", "Преимущество при испытаниях ([1])."),
        ["Disadvantage.SavingThrow"] =      new("Disadvantage on [1] Saving Throws.", "Помеха при испытаниях ([1])."),
        ["Advantage.Skill"] =               new("Advantage on [1] Checks.", "Преимущество при проверках ([1])."),
        ["Disadvantage.Skill"] =            new("Disadvantage on [1] Checks.", "Помеха при проверках ([1])."),

        // RollBonus (uses FormatNumeric — [1] already has +/- prefix)
        ["RollBonus.Attack"] =              new("[1] Attack Rolls.", "[1] к броску атаки."),
        ["RollBonus.SavingThrow"] =         new("[1] Saving Throws.", "[1] к испытаниям."),
        ["RollBonus.SavingThrowOf"] =       new("[1] [2] Saving Throws.", "[1] к испытаниям ([2])."),
        ["RollBonus.DeathSavingThrow"] =    new("[1] Death Saving Throws.", "[1] к испытаниям от смерти."),

        // Skill(Skill, Amount) — [1] is amount, [2] is skill
        ["Skill"] =                         new("[1] to [2] Checks.", "[1] к проверкам ([2])."),

        // Ability(Ability, Amount, [Cap]) — [1] is amount, [2] is ability
        ["Ability"] =                       new("[2] [1]", "[2] [1]"),

        // Proficiency(Group)
        ["Proficiency"] =                   new("Grants Proficiency with [1].", "Даёт умение: [1]."),

        // WeaponProperty.Magical (most common item meta-flag)
        ["WeaponProperty.Magical"] =        new("Counts as Magical for overcoming damage resistance.", "Считается магическим для преодоления устойчивости."),

        // WeaponDamage(Amount, [Type])
        ["WeaponDamage"] =                  new("[1] Weapon Damage.", "[1] к урону оружия."),
        ["WeaponDamage.Typed"] =            new("[1] [2] damage on weapon attacks.", "[1] урона ([2]) при атаках оружием."),

        // IncreaseMaxHP(formula)
        ["IncreaseMaxHP"] =                 new("Maximum Hit Points [1].", "Максимум очков здоровья [1]."),

        // Initiative(N)
        ["Initiative"] =                    new("[1] Initiative.", "[1] к инициативе."),

        // SpellSaveDC(N)
        ["SpellSaveDC"] =                   new("[1] Spell Save DC.", "[1] к КС заклинаний."),

        // TemporaryHP(formula)
        ["TemporaryHP"] =                   new("Grants [1] Temporary Hit Points.", "Даёт [1] временных очков здоровья."),

        // Reroll(Type, Below, Always) — [1] is type, [2] is threshold
        ["Reroll"] =                        new("Reroll [1] rolls of [2] or below.", "Переброс [1] бросков ≤ [2]."),
        ["Reroll.Always"] =                 new("Always reroll [1] rolls of [2] or below.", "Всегда перебрасывать [1] ≤ [2]."),

        // IgnoreResistance(DamageType, Level)
        ["IgnoreResistance"] =              new("Ignore [1] Resistance.", "Игнорирует устойчивость к ([1])."),

        // Savant(School)
        ["Savant"] =                        new("Savant of [1].", "Знаток школы [1]."),

        // Single-arg named boosts
        ["StatusImmunity"] =                new("Immune to [1].", "Невосприимчивость к ([1])."),

        // Zero-arg flag boosts
        ["Invulnerable"] =                  new("Invulnerable.", "Неуязвимость."),
        ["CannotBeDisarmed"] =              new("Cannot be disarmed.", "Не может быть обезоружен."),
        ["BlockSpellCast"] =                new("Cannot cast spells.", "Не может произносить заклинания."),
        ["CriticalDamageOnHit"] =           new("All hits deal critical damage.", "Все попадания наносят критический урон."),

        // Saving Throws (generic)
        ["SavingThrow"] =                   new("[1] Saving Throws", "Испытания: [1]"),

        // Armor type labels
        ["ArmorType.Clothing"] =            new("Clothing", "Ткань"),

        // Weapon skill
        ["WeaponSkill"] =                   new("Weapon Skill", "Оружейный навык"),
        ["WeaponSkills"] =                  new("Weapon Skills", "Оружейные навыки"),

        // Attack label
        ["Attack"] =                        new("Attack", "Атака"),

        // Encumbrance warnings
        ["Encumber"] =                      new("Will Encumber [1]", "[1] получит перегрузку"),
        ["HeavyEncumber"] =                 new("Will Heavily Encumber [1]", "[1] получит сильную перегрузку"),
        ["ExceedCapacity"] =                new("Will exceed [1]'s carrying capacity", "[1] превысит лимит веса"),
        ["CapacityExceeded"] =              new("Carrying Capacity Exceeded", "Превышена грузоподъемность"),
    };

    /// <summary>
    /// Tries to resolve an engine-style description for a parsed boost.
    /// Returns null if no engine description matches.
    /// </summary>
    public static EngineBoostDesc? GetEngineDescription(string funcName, string[] args)
    {
        // Resistance(DmgType, Flags) → key by flags
        if (funcName == "Resistance" && args.Length >= 2)
        {
            var flags = args[1].Trim();
            if (flags is "Resistant" or "ResistantToMagical" or "ResistantToNonMagical")
                return EngineDescriptions.GetValueOrDefault("Resistance.Resistant");
            if (flags is "Immune" or "ImmuneToMagical" or "ImmuneToNonMagical")
                return EngineDescriptions.GetValueOrDefault("Resistance.Immune");
            if (flags is "Vulnerable" or "VulnerableToMagical" or "VulnerableToNonMagical")
                return EngineDescriptions.GetValueOrDefault("Resistance.Vulnerable");
        }

        // AC(value)
        if (funcName == "AC")
            return EngineDescriptions.GetValueOrDefault("AC");

        // WeaponEnchantment(level)
        if (funcName == "WeaponEnchantment")
            return EngineDescriptions.GetValueOrDefault("WeaponEnchantment");

        // AbilityOverrideMinimum(Ability, Min)
        if (funcName == "AbilityOverrideMinimum" && args.Length >= 1)
        {
            var ability = args[0].Trim();
            return EngineDescriptions.GetValueOrDefault($"AbilityOverrideMinimum.{ability}");
        }

        // CriticalHit(Type, Result, When) — 6 meaningful combinations
        if (funcName == "CriticalHit" && args.Length >= 3)
        {
            var type = args[0].Trim();
            var result = args[1].Trim();
            var when = args[2].Trim();
            var alwaysOn = when is "Always" or "ForcedAlways";
            if (type == "AttackTarget" && result == "Success" && when == "Never")
                return EngineDescriptions.GetValueOrDefault("CriticalHit.NoCrit");
            if (type == "AttackTarget" && result == "Success" && alwaysOn)
                return EngineDescriptions.GetValueOrDefault("CriticalHit.TargetAlwaysCrit");
            if (type == "AttackRoll" && result == "Success" && alwaysOn)
                return EngineDescriptions.GetValueOrDefault("CriticalHit.Success");
            if (type == "AttackRoll" && result == "Success" && when == "Never")
                return EngineDescriptions.GetValueOrDefault("CriticalHit.NoCritHit");
            if (type == "AttackRoll" && result == "Failure" && when == "Never")
                return EngineDescriptions.GetValueOrDefault("CriticalHit.NoCritMiss");
            if (type == "AttackRoll" && result == "Failure" && alwaysOn)
                return EngineDescriptions.GetValueOrDefault("CriticalHit.AlwaysCritFail");
        }

        // Advantage/Disadvantage variants
        if (funcName is "Advantage" or "Disadvantage" && args.Length >= 1)
        {
            var ctx = args[0].Trim();
            // Two-arg variants: Advantage(Ability|SavingThrow|Skill, X)
            if (args.Length >= 2 && ctx is "Ability" or "SavingThrow" or "Skill")
                return EngineDescriptions.GetValueOrDefault($"{funcName}.{ctx}");
            // Single-arg variants: Advantage(AttackRoll), Advantage(AllSavingThrows), etc.
            if (ctx is "AttackRoll" or "AttackTarget" or "AllSavingThrows" or "AllAbilities"
                    or "AllSkills" or "Concentration" or "DeathSavingThrow")
                return EngineDescriptions.GetValueOrDefault($"{funcName}.{ctx}");
        }

        // RollBonus(Type, Amount, [AbilityOrSkill])
        if (funcName == "RollBonus" && args.Length >= 2)
        {
            var rollType = args[0].Trim();
            if (rollType == "Attack")
                return EngineDescriptions.GetValueOrDefault("RollBonus.Attack");
            if (rollType == "DeathSavingThrow")
                return EngineDescriptions.GetValueOrDefault("RollBonus.DeathSavingThrow");
            if (rollType == "SavingThrow")
                return args.Length >= 3 && !string.IsNullOrEmpty(args[2].Trim())
                    ? EngineDescriptions.GetValueOrDefault("RollBonus.SavingThrowOf")
                    : EngineDescriptions.GetValueOrDefault("RollBonus.SavingThrow");
        }

        // Skill(Skill, Amount)
        if (funcName == "Skill" && args.Length >= 2)
            return EngineDescriptions.GetValueOrDefault("Skill");

        // Ability(Ability, Amount, [Cap])
        if (funcName == "Ability" && args.Length >= 2)
            return EngineDescriptions.GetValueOrDefault("Ability");

        // Proficiency(Group)
        if (funcName == "Proficiency" && args.Length >= 1)
            return EngineDescriptions.GetValueOrDefault("Proficiency");

        // WeaponProperty(Flag) — only Magical has a human-readable description
        if (funcName == "WeaponProperty" && args.Length >= 1)
            return EngineDescriptions.GetValueOrDefault($"WeaponProperty.{args[0].Trim()}");

        // WeaponDamage(Amount, [Type])
        if (funcName == "WeaponDamage" && args.Length >= 1)
            return args.Length >= 2 && !string.IsNullOrEmpty(args[1].Trim())
                ? EngineDescriptions.GetValueOrDefault("WeaponDamage.Typed")
                : EngineDescriptions.GetValueOrDefault("WeaponDamage");

        // Single-param numeric boosts
        if (funcName is "IncreaseMaxHP" or "Initiative" or "SpellSaveDC" or "TemporaryHP"
            && args.Length >= 1)
            return EngineDescriptions.GetValueOrDefault(funcName);

        // Reroll(Type, Below, [Always])
        if (funcName == "Reroll" && args.Length >= 2)
        {
            var always = args.Length >= 3 && args[2].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            return EngineDescriptions.GetValueOrDefault(always ? "Reroll.Always" : "Reroll");
        }

        // Single-enum-arg boosts
        if (funcName is "IgnoreResistance" or "Savant" or "StatusImmunity" && args.Length >= 1)
            return EngineDescriptions.GetValueOrDefault(funcName);

        // Zero-arg flag boosts
        if (funcName is "Invulnerable" or "CannotBeDisarmed" or "BlockSpellCast"
            or "CriticalDamageOnHit")
            return EngineDescriptions.GetValueOrDefault(funcName);

        return null;
    }

    /// <summary>
    /// Formats a single boost in game-engine style using EngineDescriptions.
    /// Returns null if no engine description matches (caller should fall back to FormatSingleBoost).
    /// The [1] placeholder is replaced with the localized parameter value.
    /// </summary>
    public static string? FormatBoostEngineStyle(string funcName, string[] args, Func<string, string>? translate)
    {
        var desc = GetEngineDescription(funcName, args);
        if (desc == null) return null;

        // Pick language: check if translate returns Russian for a known key
        var lang = translate != null ? translate("_lang") : "en";
        var template = lang == "ru" ? desc.Ru : desc.En;

        // Two-placeholder boosts — substitute both [1] and [2] then return
        if (funcName == "RollBonus" && args.Length >= 3 && args[0].Trim() == "SavingThrow"
            && !string.IsNullOrEmpty(args[2].Trim()))
            return template.Replace("[1]", FormatNumeric(args[1].Trim()))
                           .Replace("[2]", Tr($"enum.{args[2].Trim()}", translate));
        if (funcName == "Skill" && args.Length >= 2)
            return template.Replace("[1]", FormatNumeric(args[1].Trim()))
                           .Replace("[2]", Tr($"enum.{args[0].Trim()}", translate));
        if (funcName == "Ability" && args.Length >= 2)
            return template.Replace("[1]", FormatNumeric(args[1].Trim()))
                           .Replace("[2]", Tr($"enum.{args[0].Trim()}", translate));
        if (funcName == "WeaponDamage" && args.Length >= 2 && !string.IsNullOrEmpty(args[1].Trim()))
            return template.Replace("[1]", FormatNumeric(args[0].Trim()))
                           .Replace("[2]", Tr($"enum.{args[1].Trim()}", translate));
        if (funcName == "Reroll" && args.Length >= 2)
            return template.Replace("[1]", Tr($"enum.{args[0].Trim()}", translate))
                           .Replace("[2]", args[1].Trim());

        // Determine the [1] value for single-placeholder boosts
        string paramValue = "";
        if (funcName == "Resistance" && args.Length >= 1)
            paramValue = Tr($"enum.{args[0].Trim()}", translate);
        else if (funcName == "AC" && args.Length >= 1)
            paramValue = FormatNumeric(args[0].Trim());
        else if (funcName == "WeaponEnchantment" && args.Length >= 1)
            paramValue = $"+{args[0].Trim()}";
        else if (funcName == "AbilityOverrideMinimum" && args.Length >= 2)
            paramValue = args[1].Trim();
        else if (funcName is "Advantage" or "Disadvantage" && args.Length >= 2)
            paramValue = Tr($"enum.{args[1].Trim()}", translate);
        else if (funcName == "RollBonus" && args.Length >= 2)
            paramValue = FormatNumeric(args[1].Trim());
        else if (funcName == "Proficiency" && args.Length >= 1)
            paramValue = Tr($"enum.{args[0].Trim()}", translate);
        else if (funcName is "IncreaseMaxHP" or "Initiative" or "SpellSaveDC" or "TemporaryHP"
                 && args.Length >= 1)
            paramValue = FormatNumeric(args[0].Trim());
        else if (funcName == "WeaponDamage" && args.Length >= 1)
            paramValue = FormatNumeric(args[0].Trim());
        else if (funcName is "IgnoreResistance" or "Savant" or "StatusImmunity" && args.Length >= 1)
            paramValue = Tr($"enum.{args[0].Trim()}", translate);

        return template.Replace("[1]", paramValue);
    }

    // ═══════════════════════════════════════════════════════════
    // PREVIEW FORMATTING — human-readable boost display
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Converts raw boost string into human-readable lines.
    /// Uses engine-style descriptions where available, falls back to label+value format.
    /// Pass a translate function to resolve enum values and boost labels via loca.
    /// Key format: "enum.XXX" for enum values, "boost.FuncName" for boost labels, "_lang" for language code.
    /// </summary>
    public static string FormatBoostsForPreview(string rawBoosts, Func<string, string>? translate = null)
    {
        if (string.IsNullOrWhiteSpace(rawBoosts)) return "";
        var parts = rawBoosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lines = new List<string>();
        foreach (var part in parts)
        {
            var line = FormatSingleBoost(part, translate);
            if (!string.IsNullOrEmpty(line))
                lines.Add(line);
        }
        return string.Join("\n", lines);
    }

    private static string Tr(string key, Func<string, string>? translate)
    {
        if (translate == null) return key;
        var result = translate(key);
        return result != key ? result : key; // fallback to raw if no translation
    }

    private static string FormatSingleBoost(string raw, Func<string, string>? translate)
    {
        // Skip IF(...) wrappers — show the inner boost
        if (raw.StartsWith("IF(", StringComparison.OrdinalIgnoreCase))
            return ""; // complex conditions not shown in simple preview

        var parsed = ParseBoostCall(raw);
        if (parsed == null) return raw;
        var (funcName, args) = parsed.Value;

        // Try engine-style description first (matches how BG3 displays boosts in-game)
        var engineLine = FormatBoostEngineStyle(funcName, args, translate);
        if (engineLine != null)
            return engineLine;

        var def = FindBoost(funcName);
        if (def == null) return raw; // unknown boost — show raw

        var label = Tr($"boost.{funcName}", translate);
        // If no translation found, use built-in English label
        if (label == $"boost.{funcName}")
            label = def.Label;

        var valueParts = new List<string>();

        for (int i = 0; i < def.Params.Length && i < args.Length; i++)
        {
            var val = args[i].Trim();
            if (string.IsNullOrEmpty(val)) continue;

            var param = def.Params[i];
            if (param.Type == "hidden") continue;

            if (param.Type == "optnum")
            {
                // Optional cap: skip if empty/0, show as "(up to N)" otherwise
                if (!string.IsNullOrEmpty(val) && val != "0")
                    valueParts.Add($"(up to {val})");
            }
            else if (param.Type is "number" or "formula" or "float")
            {
                valueParts.Add(FormatNumeric(val));
            }
            else if (param.Type == "optbool")
            {
                // skip in preview
            }
            else if (param.Type == "dice")
            {
                valueParts.Add(val);
            }
            else if (param.Type == "bool")
            {
                // skip
            }
            else if (param.Type == "string")
            {
                // Show spell/status/interrupt names with localization
                if (funcName is "UnlockSpell" or "UnlockInterrupt" or "UnlockSpellVariant"
                    or "ApplyStatus" or "RemoveStatus" or "StatusImmunity")
                {
                    var resolved = Tr($"stat.{val}", translate);
                    valueParts.Add(resolved != $"stat.{val}" ? resolved : val);
                }
            }
            else // enum
            {
                valueParts.Add(Tr($"enum.{val}", translate));
            }
        }

        if (valueParts.Count == 0)
            return label;

        return $"{label} {string.Join(" ", valueParts)}";
    }

    private static string FormatNumeric(string val)
    {
        // Try parse as number for +/- formatting
        if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var num))
        {
            if (num >= 0) return $"+{val}";
            return val; // already has minus
        }
        // Formula like "ProficiencyBonus" — keep as-is
        return val;
    }

    // ═══════════════════════════════════════════════════════════
    // LABEL METHODS (keep for UI)
    // ═══════════════════════════════════════════════════════════

    public static string ArmorTypeLabel(string type) => type switch
    {
        "None" => "None / Нет", "Cloth" => "Cloth / Ткань", "Padded" => "Padded / Стёганая",
        "Leather" => "Leather / Кожаная", "StuddedLeather" => "Studded Leather / Проклёпанная кожа",
        "Hide" => "Hide / Шкурная", "ChainShirt" => "Chain Shirt / Кольч. рубаха",
        "ScaleMail" => "Scale Mail / Чешуйчатая", "BreastPlate" => "Breastplate / Нагрудник",
        "HalfPlate" => "Half Plate / Полулаты", "RingMail" => "Ring Mail / Кольчуга",
        "ChainMail" => "Chain Mail / Кольчужная", "Splint" => "Splint / Шинная",
        "Plate" => "Plate / Латная", _ => type
    };

    public static string ProficiencyLabel(string prof) => prof switch
    {
        "LightArmor" => "Light Armor / Лёгкая броня", "MediumArmor" => "Medium Armor / Средняя броня",
        "HeavyArmor" => "Heavy Armor / Тяжёлая броня", "Shields" => "Shields / Щиты",
        "SimpleMeleeWeapon" => "Simple Melee / Простое ближнее", "SimpleRangedWeapon" => "Simple Ranged / Простое дальнобойное",
        "MartialMeleeWeapon" => "Martial Melee / Воинское ближнее", "MartialRangedWeapon" => "Martial Ranged / Воинское дальнобойное",
        _ => prof
    };

    public static string WeaponPropertyLabel(string prop) => prop switch
    {
        "Finesse" => "Finesse / Фехтовальное", "Light" => "Light / Лёгкое", "Heavy" => "Heavy / Тяжёлое",
        "Melee" => "Melee / Ближний бой", "Magical" => "Magical / Магическое", "Reach" => "Reach / Досягаемость",
        "Thrown" => "Thrown / Метательное", "Twohanded" => "Two-handed / Двуручное",
        "Versatile" => "Versatile / Универсальное", _ => prop
    };

    public static readonly string[] ArmorTypeLabels = ArmorTypes.Select(ArmorTypeLabel).ToArray();
    public static readonly string[] ProficiencyLabels = ProficiencyTypes.Select(ProficiencyLabel).ToArray();
}
