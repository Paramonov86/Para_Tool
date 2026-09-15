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

    /// <summary>
    /// Equip slot as the raw BG3 stats value ("Cloak", "Helmet", "Ring", "Melee Main Weapon", …),
    /// resolved from the base item's using-chain. Written explicitly so the item never falls back
    /// to the wrong slot (Helmet) when stat-inheritance / load-order fails to resolve it.
    /// Null = don't emit (let inheritance decide).
    /// </summary>
    public string? Slot { get; set; }

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

    /// <summary>Weapon-specific: Damage Type override (null = inherit, e.g. "Slashing").</summary>
    public string? DamageType { get; set; }

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

    /// <summary>Edited copies of creatures this artifact's spell cards summon.</summary>
    public List<SummonDefinition> Summons { get; set; } = [];

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

    // ─── Spell/Status Renames ──────────────────────────────

    /// <summary>Spell display name overrides: original StatId → new display name per language.</summary>
    public Dictionary<string, Dictionary<string, string>> SpellRenames { get; set; } = [];

    /// <summary>Status display name overrides: original StatId → new display name per language.</summary>
    public Dictionary<string, Dictionary<string, string>> StatusRenames { get; set; } = [];

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

    // ─── Tombstones (deliberately removed inherited entries) ──

    /// <summary>
    /// Passive names the user explicitly removed from this item relative to its base.
    /// Filtered out in ArtifactCompiler so they don't resurrect from inherited PassivesOnEquip.
    /// </summary>
    public List<string> RemovedPassives { get; set; } = [];

    /// <summary>Spell names the user explicitly removed.</summary>
    public List<string> RemovedSpells { get; set; } = [];

    /// <summary>Status names the user explicitly removed.</summary>
    public List<string> RemovedStatuses { get; set; } = [];

    /// <summary>Raw boost calls the user explicitly removed (e.g. "WeaponEnchantment(1)").</summary>
    public List<string> RemovedBoosts { get; set; } = [];

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

    /// <summary>
    /// Copy every content field from <paramref name="other"/> into this instance, while
    /// preserving this artifact's own on-disk identity (<see cref="ArtifactId"/> and
    /// <see cref="TemplateUuid"/>). Used by Reset, item-level Undo/Redo and version revert.
    /// NOTE: takes ownership of <paramref name="other"/>'s collection references — callers
    /// pass throwaway instances (a fresh build or a deserialized snapshot), so the source is
    /// never reused afterwards.
    /// </summary>
    public void CopyFrom(ArtifactDefinition other)
    {
        FormatVersion = other.FormatVersion;
        CreatedAt = other.CreatedAt;
        // Identity (ArtifactId / TemplateUuid) intentionally NOT copied — keep our own.

        StatId = other.StatId;
        StatType = other.StatType;
        UsingBase = other.UsingBase;
        Slot = other.Slot;
        ParentTemplateUuid = other.ParentTemplateUuid;

        Rarity = other.Rarity;
        ComboCategory = other.ComboCategory;
        ValueOverride = other.ValueOverride;
        Unique = other.Unique;
        Weight = other.Weight;
        ArmorClass = other.ArmorClass;
        ArmorType = other.ArmorType;
        ProficiencyGroup = other.ProficiencyGroup;
        Damage = other.Damage;
        DamageType = other.DamageType;
        VersatileDamage = other.VersatileDamage;
        DefaultBoosts = other.DefaultBoosts;
        WeaponProperties = other.WeaponProperties;

        Boosts = other.Boosts;
        PassivesOnEquip = other.PassivesOnEquip;
        StatusOnEquip = other.StatusOnEquip;
        SpellsOnEquip = other.SpellsOnEquip;
        BoostsOnEquipMainHand = other.BoostsOnEquipMainHand;
        BoostsOnEquipOffHand = other.BoostsOnEquipOffHand;

        Passives = other.Passives;
        Statuses = other.Statuses;
        Spells = other.Spells;
        Summons = other.Summons;

        DisplayName = other.DisplayName;
        Description = other.Description;
        DescriptionParams = other.DescriptionParams;
        DisplayNameHandle = other.DisplayNameHandle;
        DescriptionHandle = other.DescriptionHandle;

        SpellRenames = other.SpellRenames;
        StatusRenames = other.StatusRenames;

        IconMainDdsBase64 = other.IconMainDdsBase64;
        IconConsoleDdsBase64 = other.IconConsoleDdsBase64;
        AtlasIconMapKey = other.AtlasIconMapKey;

        RemovedPassives = other.RemovedPassives;
        RemovedSpells = other.RemovedSpells;
        RemovedStatuses = other.RemovedStatuses;
        RemovedBoosts = other.RemovedBoosts;

        PatchEnabled = other.PatchEnabled;
        AddToLoot = other.AddToLoot;
        LootPool = other.LootPool;
        LootThemes = other.LootThemes;
    }
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
/// A card cloned from an existing spell compiles either as a new spell (renamed per artifact,
/// <c>using</c> the original) or, with <see cref="EditOriginal"/>, as an override of the original.
/// Fields not on the card stay inherited through <c>using</c>.
/// </summary>
public sealed class SpellDefinition
{
    public string Name { get; set; } = "";
    public string? UsingBase { get; set; }

    /// <summary>
    /// Keep the original name and override the original entry — the change applies everywhere
    /// the spell is used, not only on this item.
    /// </summary>
    public bool EditOriginal { get; set; }

    /// <summary>Shout, Target, Projectile, Zone, etc.</summary>
    public string SpellType { get; set; } = "Shout";

    public Dictionary<string, string> DisplayName { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DisplayNameHandle { get; set; } = "";

    public Dictionary<string, string> Description { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DescriptionHandle { get; set; } = "";
    public string DescriptionParams { get; set; } = "";

    /// <summary>Loca handles of the spell the card was cloned from.</summary>
    public string? SourceDisplayNameHandle { get; set; }
    public string? SourceDescriptionHandle { get; set; }

    /// <summary>
    /// The user changed the text. Until then an edited original keeps its own handles, so every
    /// language the game ships stays translated; a copy only carries the languages it was given.
    /// </summary>
    public bool DisplayNameEdited { get; set; }
    public bool DescriptionEdited { get; set; }

    public string? Icon { get; set; }

    public string SpellProperties { get; set; } = "";
    public string UseCosts { get; set; } = "";
    public string Cooldown { get; set; } = "";
    public string TargetConditions { get; set; } = "";
    public string SpellFlags { get; set; } = "";

    // Core fields added with the spell cards. Null = not on the card, inherited from the base
    // (and what older .art files load as).
    public string? Level { get; set; }
    public string? SpellSchool { get; set; }
    public string? TargetRadius { get; set; }
    public string? AreaRadius { get; set; }
    public string? SpellRoll { get; set; }
    public string? SpellSuccess { get; set; }
    public string? SpellFail { get; set; }

    /// <summary>Raw extra data fields (key → value) for uncommon properties.</summary>
    public Dictionary<string, string> ExtraData { get; set; } = [];

    /// <summary>
    /// The variants of a container spell (its <c>ContainerSpells</c>), in list order, each compiled as
    /// its own entry. They follow the container: copies are renamed after it and linked to it, and
    /// "edit original" on the container overrides them too. Empty for an ordinary spell.
    /// </summary>
    public List<SpellDefinition> Variants { get; set; } = [];

    /// <summary>This card followed by its variants.</summary>
    public IEnumerable<SpellDefinition> WithVariants() => Variants.Prepend(this);
}

/// <summary>
/// An edited copy of a summoned creature. Compiles to a Character entry <c>using</c> the
/// creature's stats and a character RootTemplate whose ParentTemplateId is the original creature,
/// so model, visuals and scripts stay the original's. Every <c>Summon(&lt;original&gt;, …)</c> in
/// this artifact's spell cards spawns the copy instead.
/// </summary>
public sealed class SummonDefinition
{
    /// <summary>New character RootTemplate UUID (generated once).</summary>
    public string TemplateUuid { get; set; } = Guid.NewGuid().ToString();

    /// <summary>The creature template the spell summoned before.</summary>
    public string ParentTemplateUuid { get; set; } = "";

    /// <summary>The original creature's Character stats entry.</summary>
    public string UsingBase { get; set; } = "";

    /// <summary>Stats entry name written; assigned by the compiler (<c>{StatId}_Summon_{n}</c>).</summary>
    public string StatsName { get; set; } = "";

    /// <summary>Card fields (Vitality, Armor, abilities, Level, Passives, DefaultBoosts) → value.</summary>
    public Dictionary<string, string> Stats { get; set; } = [];

    public Dictionary<string, string> DisplayName { get; set; } = new() { ["en"] = "", ["ru"] = "" };
    public string DisplayNameHandle { get; set; } = "";

    /// <summary>The creature was renamed; until then the template inherits the original name.</summary>
    public bool DisplayNameEdited { get; set; }
}
