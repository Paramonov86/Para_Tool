namespace ParaTool.Core.Schema;

/// <summary>
/// Category assignment for BoostMapping.Boosts and BoostMapping.Functors.
/// Keys are BlockDef.FuncName; values are stable category keys.
/// </summary>
public static class BoostCategories
{
    public const string Unknown = "Other";

    public static readonly string[] BoostCategoryOrder =
    [
        "AbilityStats", "AttackDamage", "Critical", "Resistance", "AdvantageRolls",
        "Proficiency", "SpellsMagic", "Resources", "HpHealing", "Movement",
        "Vision", "TagsFlags", "CombatMode", "Social", "Other",
    ];

    public static readonly string[] FunctorCategoryOrder =
    [
        "DamageHealing", "StatusEffects", "Resources", "SurfaceZone",
        "MovementPositioning", "SpellsCombat", "SummonSpawn", "RollManipulation",
        "Misc", "Other",
    ];

    /// <summary>FuncNames relevant to weapon DefaultBoosts (pinned section order).</summary>
    public static readonly string[] WeaponDefaultBoostWhitelist =
    [
        "WeaponEnchantment",
        "WeaponProperty",
        "WeaponDamage",
        "CharacterWeaponDamage",
        "WeaponDamageDieOverride",
        "WeaponDamageTypeOverride",
        "WeaponDamageResistance",
        "WeaponAttackRollBonus",
        "WeaponAttackTypeOverride",
        "WeaponAttackRollAbilityOverride",
        "HalveWeaponDamage",
        "IgnoreResistance",
        "CannotBeDisarmed",
        "HiddenDuringCinematic",
    ];

    public static readonly Dictionary<string, string> FuncToCategory =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ── Boosts: Ability & Stats ──
            ["AC"] = "AbilityStats",
            ["Ability"] = "AbilityStats",
            ["AbilityOverrideMinimum"] = "AbilityStats",
            ["NullifyAbilityScore"] = "AbilityStats",
            ["ACOverrideFormula"] = "AbilityStats",
            ["AddProficiencyToAC"] = "AbilityStats",
            ["AddProficiencyToDamage"] = "AbilityStats",
            ["BlockAbilityModifierFromAC"] = "AbilityStats",
            ["ProficiencyBonusOverride"] = "AbilityStats",
            ["ProficiencyBonusIncrease"] = "AbilityStats",
            ["HalveWeaponDamage"] = "AbilityStats",

            // ── Boosts: Attack & Damage ──
            ["RollBonus"] = "AttackDamage",
            ["DamageBonus"] = "AttackDamage",
            ["CharacterWeaponDamage"] = "AttackDamage",
            ["CharacterUnarmedDamage"] = "AttackDamage",
            ["WeaponDamage"] = "AttackDamage",
            ["WeaponEnchantment"] = "AttackDamage",
            ["WeaponAttackRollBonus"] = "AttackDamage",
            ["WeaponProperty"] = "AttackDamage",
            ["WeaponAttackTypeOverride"] = "AttackDamage",
            ["WeaponDamageDieOverride"] = "AttackDamage",
            ["WeaponDamageTypeOverride"] = "AttackDamage",
            ["WeaponAttackRollAbilityOverride"] = "AttackDamage",
            ["WeaponDamageResistance"] = "AttackDamage",
            ["EntityThrowDamage"] = "AttackDamage",
            ["DamageReduction"] = "AttackDamage",
            ["DamageTakenBonus"] = "AttackDamage",

            // ── Boosts: Critical Hits ──
            ["CriticalHit"] = "Critical",
            ["CriticalHitExtraDice"] = "Critical",
            ["ReduceCriticalAttackThreshold"] = "Critical",
            ["CriticalDamageOnHit"] = "Critical",

            // ── Boosts: Resistance & Immunity ──
            ["Resistance"] = "Resistance",
            ["StatusImmunity"] = "Resistance",
            ["IgnoreResistance"] = "Resistance",
            ["IgnoreDamageThreshold"] = "Resistance",
            ["SpellResistance"] = "Resistance",
            ["Invulnerable"] = "Resistance",
            ["RedirectDamage"] = "Resistance",

            // ── Boosts: Advantage & Rolls ──
            ["Advantage"] = "AdvantageRolls",
            ["Disadvantage"] = "AdvantageRolls",
            ["Reroll"] = "AdvantageRolls",
            ["MinimumRollResult"] = "AdvantageRolls",
            ["MaximumRollResult"] = "AdvantageRolls",
            ["GuaranteedChanceRollOutcome"] = "AdvantageRolls",

            // ── Boosts: Proficiency ──
            ["Proficiency"] = "Proficiency",
            ["ProficiencyBonus"] = "Proficiency",
            ["ExpertiseBonus"] = "Proficiency",
            ["Skill"] = "Proficiency",

            // ── Boosts: Spells & Magic ──
            ["SpellSaveDC"] = "SpellsMagic",
            ["UnlockSpell"] = "SpellsMagic",
            ["UnlockSpellVariant"] = "SpellsMagic",
            ["UnlockInterrupt"] = "SpellsMagic",
            ["Savant"] = "SpellsMagic",
            ["BlockSpellCast"] = "SpellsMagic",
            ["ConcentrationIgnoreDamage"] = "SpellsMagic",
            ["UseBoosts"] = "SpellsMagic",

            // ── Boosts: Resources ──
            ["ActionResource"] = "Resources",
            ["ActionResourceOverride"] = "Resources",
            ["ActionResourceMultiplier"] = "Resources",
            ["ActionResourceBlock"] = "Resources",

            // ── Boosts: HP & Healing ──
            ["IncreaseMaxHP"] = "HpHealing",
            ["TemporaryHP"] = "HpHealing",
            ["BlockRegainHP"] = "HpHealing",
            ["MaximizeHealing"] = "HpHealing",

            // ── Boosts: Movement & Physical ──
            ["Initiative"] = "Movement",
            ["ObjectSize"] = "Movement",
            ["ObjectSizeOverride"] = "Movement",
            ["ScaleMultiplier"] = "Movement",
            ["Weight"] = "Movement",
            ["CarryCapacityMultiplier"] = "Movement",
            ["JumpMaxDistanceBonus"] = "Movement",
            ["JumpMaxDistanceMultiplier"] = "Movement",
            ["FallDamageMultiplier"] = "Movement",
            ["IgnoreFallDamage"] = "Movement",
            ["IgnoreLeaveAttackRange"] = "Movement",
            ["IgnorePointBlankDisadvantage"] = "Movement",
            ["IgnoreLowGroundPenalty"] = "Movement",
            ["MovementSpeedLimit"] = "Movement",
            ["NonLethal"] = "Movement",
            ["NoAOEDamageOnLand"] = "Movement",

            // ── Boosts: Vision & Light ──
            ["DarkvisionRange"] = "Vision",
            ["DarkvisionRangeMin"] = "Vision",
            ["DarkvisionRangeOverride"] = "Vision",
            ["SightRangeAdditive"] = "Vision",
            ["SightRangeMinimum"] = "Vision",
            ["SightRangeMaximum"] = "Vision",
            ["Invisibility"] = "Vision",
            ["GameplayLight"] = "Vision",
            ["GameplayObscurity"] = "Vision",

            // ── Boosts: Tags & Flags ──
            ["Tag"] = "TagsFlags",
            ["Attribute"] = "TagsFlags",
            ["Lootable"] = "TagsFlags",
            ["ItemReturnToOwner"] = "TagsFlags",
            ["CannotBeDisarmed"] = "TagsFlags",
            ["Detach"] = "TagsFlags",

            // ── Boosts: Combat Mode ──
            ["DualWielding"] = "CombatMode",
            ["TwoWeaponFighting"] = "CombatMode",
            ["MonkWeaponDamageDiceOverride"] = "CombatMode",
            ["UnarmedMagicalProperty"] = "CombatMode",
            ["ArmorAbilityModifierCapOverride"] = "CombatMode",
            ["ProjectileDeflect"] = "CombatMode",
            ["AreaDamageEvade"] = "CombatMode",
            ["SourceAdvantageOnAttack"] = "CombatMode",
            ["DownedStatus"] = "CombatMode",

            // ── Boosts: Social / Misc ──
            ["HiddenDuringCinematic"] = "Social",
            ["DialogueBlock"] = "Social",
            ["BlockTravel"] = "Social",

            // ── Functors: Damage & Healing ──
            ["DealDamage"] = "DamageHealing",
            ["RegainHitPoints"] = "DamageHealing",
            ["GainTemporaryHitPoints"] = "DamageHealing",
            ["RegainTemporaryHitPoints"] = "DamageHealing",

            // ── Functors: Status Effects ──
            ["ApplyStatus"] = "StatusEffects",
            ["ApplyEquipmentStatus"] = "StatusEffects",
            ["RemoveStatus"] = "StatusEffects",
            ["RemoveUniqueStatus"] = "StatusEffects",
            ["SetStatusDuration"] = "StatusEffects",
            ["RemoveStatusByLevel"] = "StatusEffects",
            ["RemoveAuraByChildStatus"] = "StatusEffects",

            // ── Functors: Resources ──
            ["RestoreResource"] = "Resources",
            ["UseActionResource"] = "Resources",

            // ── Functors: Surface & Zone ──
            ["CreateSurface"] = "SurfaceZone",
            ["CreateConeSurface"] = "SurfaceZone",
            ["SurfaceChange"] = "SurfaceZone",
            ["CreateZone"] = "SurfaceZone",
            ["SurfaceClearLayer"] = "SurfaceZone",
            ["CreateWall"] = "SurfaceZone",

            // ── Functors: Movement & Positioning ──
            ["Force"] = "MovementPositioning",
            ["DoTeleport"] = "MovementPositioning",
            ["TeleportSource"] = "MovementPositioning",
            ["SwapPlaces"] = "MovementPositioning",
            ["Knockback"] = "MovementPositioning",

            // ── Functors: Spells & Combat ──
            ["UseSpell"] = "SpellsCombat",
            ["UseAttack"] = "SpellsCombat",
            ["ExecuteWeaponFunctors"] = "SpellsCombat",
            ["Counterspell"] = "SpellsCombat",
            ["BreakConcentration"] = "SpellsCombat",
            ["ResetCooldowns"] = "SpellsCombat",
            ["CreateExplosion"] = "SpellsCombat",
            ["FireProjectile"] = "SpellsCombat",
            ["SpawnExtraProjectiles"] = "SpellsCombat",

            // ── Functors: Summon & Spawn ──
            ["Summon"] = "SummonSpawn",
            ["SummonInInventory"] = "SummonSpawn",
            ["Unsummon"] = "SummonSpawn",
            ["Spawn"] = "SummonSpawn",
            ["SpawnInInventory"] = "SummonSpawn",

            // ── Functors: Roll Manipulation ──
            ["AdjustRoll"] = "RollManipulation",
            ["SetRoll"] = "RollManipulation",
            ["SetReroll"] = "RollManipulation",
            ["SetAdvantage"] = "RollManipulation",
            ["SetDisadvantage"] = "RollManipulation",
            ["MaximizeRoll"] = "RollManipulation",
            ["SetDamageResistance"] = "RollManipulation",

            // ── Functors: Misc ──
            ["Resurrect"] = "Misc",
            ["Stabilize"] = "Misc",
            ["Kill"] = "Misc",
            ["Douse"] = "Misc",
            ["DisarmWeapon"] = "Misc",
            ["ShortRest"] = "Misc",
            ["TriggerRandomCast"] = "Misc",
            ["SwitchDeathType"] = "Misc",
            ["DisarmAndStealWeapon"] = "Misc",
            ["Unlock"] = "Misc",
            ["Sabotage"] = "Misc",
            ["Pickup"] = "Misc",
            ["Drop"] = "Misc",
            ["ResetCombatTurn"] = "Misc",
            ["CameraWait"] = "Misc",
            ["TutorialEvent"] = "Misc",
        };

    public static string GetCategory(string funcName)
        => FuncToCategory.TryGetValue(funcName, out var cat) ? cat : Unknown;
}
