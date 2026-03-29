namespace ParaTool.Core.Artifacts;

/// <summary>
/// Complete artifact definition — the root model for .art files.
/// Contains everything needed to generate a BG3 item:
/// Stats, RootTemplate, Localization, Icons, Passives, Statuses.
///
/// Template + Stats are treated as ONE entity for the user.
/// The user picks a base item to inherit from, gets its model/visuals,
/// and customizes mechanics through this definition.
/// </summary>
public sealed class ArtifactDefinition
{
    /// <summary>Format version for forward compatibility.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>When this artifact was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When this artifact was last modified.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // ─── Identity ───────────────────────────────────────────

    /// <summary>Unique ID for this artifact (used internally by ParaTool).</summary>
    public string ArtifactId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Stats entry name (e.g. "AMP_Custom_Sword_01").</summary>
    public string StatId { get; set; } = "";

    /// <summary>Stats type: "Armor" or "Weapon".</summary>
    public string StatType { get; set; } = "Armor";

    /// <summary>
    /// The base entry to inherit from via "using" (e.g. "ARM_Gloves_Metal", "WPN_Longsword_1").
    /// Determines the 3D model, slot, base properties.
    /// </summary>
    public string UsingBase { get; set; } = "";

    // ─── Template ───────────────────────────────────────────

    /// <summary>
    /// New unique RootTemplate UUID for this artifact.
    /// Generated once, never changes.
    /// </summary>
    public string TemplateUuid { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// RootTemplate UUID of the parent template to inherit visuals from.
    /// Resolved from the UsingBase's RootTemplate.
    /// </summary>
    public string ParentTemplateUuid { get; set; } = "";

    // ─── Basic Properties ───────────────────────────────────

    public string Rarity { get; set; } = "Uncommon";
    public string ComboCategory { get; set; } = "d1";
    public int ValueOverride { get; set; } = 100;
    public bool Unique { get; set; } = true;
    public double Weight { get; set; } = -1; // -1 = inherit from base

    /// <summary>Armor-specific: ArmorClass override (-1 = inherit).</summary>
    public int ArmorClass { get; set; } = -1;

    /// <summary>Armor-specific: ArmorType override (null = inherit).</summary>
    public string? ArmorType { get; set; }

    /// <summary>Armor-specific: Proficiency Group override (null = inherit).</summary>
    public string? ProficiencyGroup { get; set; }

    /// <summary>Weapon-specific: Damage dice (null = inherit, e.g. "1d8").</summary>
    public string? Damage { get; set; }

    /// <summary>Weapon-specific: Versatile damage (null = inherit).</summary>
    public string? VersatileDamage { get; set; }

    /// <summary>Weapon-specific: DefaultBoosts (e.g. "WeaponEnchantment(2);WeaponProperty(Magical)").</summary>
    public string? DefaultBoosts { get; set; }

    /// <summary>Weapon-specific: Weapon Properties (null = inherit).</summary>
    public string? WeaponProperties { get; set; }

    // ─── Mechanics ──────────────────────────────────────────

    /// <summary>Boosts applied directly on the item (auto-localized by BG3).</summary>
    public string Boosts { get; set; } = "";

    /// <summary>Names of passives applied on equip (semicolon-separated).</summary>
    public string PassivesOnEquip { get; set; } = "";

    /// <summary>Status IDs applied on equip (semicolon-separated).</summary>
    public string StatusOnEquip { get; set; } = "";

    /// <summary>Spells unlocked by this item (semicolon-separated).</summary>
    public string SpellsOnEquip { get; set; } = "";

    /// <summary>Weapon-specific: Boosts when equipped in main hand (e.g. "UnlockSpell(Target_Sickle_l)").</summary>
    public string? BoostsOnEquipMainHand { get; set; }

    /// <summary>Weapon-specific: Boosts when equipped in off hand.</summary>
    public string? BoostsOnEquipOffHand { get; set; }

    // ─── Passive Definitions ────────────────────────────────

    /// <summary>Custom passives created for this artifact.</summary>
    public List<PassiveDefinition> Passives { get; set; } = [];

    // ─── Status Definitions ─────────────────────────────────

    /// <summary>Custom statuses created for this artifact.</summary>
    public List<StatusDefinition> Statuses { get; set; } = [];

    // ─── Spell Definitions ──────────────────────────────────

    /// <summary>Custom spells/abilities created for this artifact.</summary>
    public List<SpellDefinition> Spells { get; set; } = [];

    // ─── Localization ───────────────────────────────────────

    /// <summary>Item display name (per language, BB-code format).</summary>
    public Dictionary<string, string> DisplayName { get; set; } = new()
    {
        ["en"] = "", ["ru"] = ""
    };

    /// <summary>Item description (per language, BB-code format).</summary>
    public Dictionary<string, string> Description { get; set; } = new()
    {
        ["en"] = "", ["ru"] = ""
    };

    /// <summary>DescriptionParams for dynamic values (e.g. "DealDamage(1d4,Fire);2").</summary>
    public string DescriptionParams { get; set; } = "";

    /// <summary>Handle for DisplayName (generated once).</summary>
    public string DisplayNameHandle { get; set; } = "";

    /// <summary>Handle for Description (generated once).</summary>
    public string DescriptionHandle { get; set; } = "";

    // ─── Icons ──────────────────────────────────────────────

    /// <summary>Custom icon: 380×380 DDS BC3 (base64-encoded, null = use atlas icon).</summary>
    public string? IconMainDdsBase64 { get; set; }

    /// <summary>Custom icon: 144×144 DDS BC3 (base64-encoded, null = use atlas icon).</summary>
    public string? IconConsoleDdsBase64 { get; set; }

    /// <summary>
    /// If using an existing icon from an atlas: the MapKey name.
    /// When set, IconMainDds/IconConsoleDds are null — the existing icon is reused.
    /// </summary>
    public string? AtlasIconMapKey { get; set; }

    // ─── Patching ────────────────────────────────────────────

    /// <summary>Whether this artifact should be patched into the game at all.</summary>
    public bool PatchEnabled { get; set; } = true;

    // ─── Loot Integration ───────────────────────────────────

    /// <summary>Whether this artifact should be added to AMP loot tables.</summary>
    public bool AddToLoot { get; set; } = true;

    /// <summary>Pool for loot tables (e.g. "Weapons", "Armor", "Rings").</summary>
    public string? LootPool { get; set; }

    /// <summary>Themes for thematic loot tables.</summary>
    public List<string> LootThemes { get; set; } = [];
}

/// <summary>
/// Custom passive (PassiveData) definition within an artifact.
/// </summary>
public sealed class PassiveDefinition
{
    public string Name { get; set; } = "";
    public string? UsingBase { get; set; }

    public string Properties { get; set; } = "Highlighted";

    /// <summary>Display name per language (BB-code).</summary>
    public Dictionary<string, string> DisplayName { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DisplayNameHandle { get; set; } = "";

    /// <summary>Description per language (BB-code).</summary>
    public Dictionary<string, string> Description { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DescriptionHandle { get; set; } = "";
    public string DescriptionParams { get; set; } = "";

    /// <summary>Icon name (from atlas or custom).</summary>
    public string? Icon { get; set; }

    // Boost-based passive
    public string BoostContext { get; set; } = "";
    public string BoostConditions { get; set; } = "";
    public string Boosts { get; set; } = "";

    // Functor-based passive
    public string StatsFunctorContext { get; set; } = "";
    public string Conditions { get; set; } = "";
    public string StatsFunctors { get; set; } = "";
}

/// <summary>
/// Custom status (StatusData) definition within an artifact.
/// </summary>
public sealed class StatusDefinition
{
    public string Name { get; set; } = "";
    public string? UsingBase { get; set; }

    /// <summary>BOOST or EFFECT.</summary>
    public string StatusType { get; set; } = "BOOST";

    public Dictionary<string, string> DisplayName { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DisplayNameHandle { get; set; } = "";

    public Dictionary<string, string> Description { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DescriptionHandle { get; set; } = "";
    public string DescriptionParams { get; set; } = "";

    public string? Icon { get; set; }

    public string StatusPropertyFlags { get; set; } = "";
    public string StatusGroups { get; set; } = "";
    public string StackType { get; set; } = "Overwrite";
    public int StackPriority { get; set; } = 0;

    public string Boosts { get; set; } = "";
    public string PassivesOnApply { get; set; } = "";
    public string RemoveEvents { get; set; } = "";

    public string? StatusEffect { get; set; }
    public string? SoundVocalStart { get; set; }
    public string? SoundVocalEnd { get; set; }
}

/// <summary>
/// Custom spell/ability definition within an artifact.
/// </summary>
public sealed class SpellDefinition
{
    public string Name { get; set; } = "";
    public string? UsingBase { get; set; }

    /// <summary>Shout, Target, Projectile, Zone, etc.</summary>
    public string SpellType { get; set; } = "Shout";

    public Dictionary<string, string> DisplayName { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DisplayNameHandle { get; set; } = "";

    public Dictionary<string, string> Description { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DescriptionHandle { get; set; } = "";
    public string DescriptionParams { get; set; } = "";

    public string? Icon { get; set; }

    public string SpellProperties { get; set; } = "";
    public string UseCosts { get; set; } = "";
    public string Cooldown { get; set; } = "";
    public string TargetConditions { get; set; } = "";
    public string SpellFlags { get; set; } = "";

    /// <summary>Raw extra data fields (key → value) for uncommon properties.</summary>
    public Dictionary<string, string> ExtraData { get; set; } = [];
}
