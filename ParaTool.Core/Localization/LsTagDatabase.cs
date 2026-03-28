namespace ParaTool.Core.Localization;

/// <summary>
/// Database of known BG3 LSTag tooltips for the GUI editor toolbar.
/// Tags are organized by category for easy toolbar button grouping.
/// </summary>
public static class LsTagDatabase
{
    public sealed class TagInfo
    {
        public required string Tooltip { get; init; }
        public string? Type { get; init; }  // null = simple tooltip, "Status", "Spell", etc.
        public required string Label { get; init; }
        public string? LabelRu { get; init; }
        public required string Category { get; init; }
    }

    /// <summary>BB-code tag name for the given LSTag type.</summary>
    public static string BbTagForType(string? type) => type switch
    {
        "Status" => "status",
        "Spell" => "spell",
        "Passive" => "passive",
        "ActionResource" => "resource",
        _ => "tip"
    };

    /// <summary>All known tooltip-only LSTag entries (no Type attribute).</summary>
    public static readonly TagInfo[] Tooltips =
    [
        // Abilities
        new() { Tooltip = "Strength", Label = "Strength", LabelRu = "Сила", Category = "Abilities" },
        new() { Tooltip = "Dexterity", Label = "Dexterity", LabelRu = "Ловкость", Category = "Abilities" },
        new() { Tooltip = "Constitution", Label = "Constitution", LabelRu = "Телосложение", Category = "Abilities" },
        new() { Tooltip = "Intelligence", Label = "Intelligence", LabelRu = "Интеллект", Category = "Abilities" },
        new() { Tooltip = "Wisdom", Label = "Wisdom", LabelRu = "Мудрость", Category = "Abilities" },
        new() { Tooltip = "Charisma", Label = "Charisma", LabelRu = "Харизма", Category = "Abilities" },

        // Combat
        new() { Tooltip = "ArmourClass", Label = "AC", LabelRu = "КБ", Category = "Combat" },
        new() { Tooltip = "HitPoints", Label = "Hit Points", LabelRu = "ОЗ", Category = "Combat" },
        new() { Tooltip = "TemporaryHitPoints", Label = "Temporary HP", LabelRu = "Врем. ОЗ", Category = "Combat" },
        new() { Tooltip = "AttackRoll", Label = "Attack Roll", LabelRu = "Бросок атаки", Category = "Combat" },
        new() { Tooltip = "CriticalHit", Label = "Critical Hit", LabelRu = "Крит. удар", Category = "Combat" },
        new() { Tooltip = "SavingThrow", Label = "Saving Throw", LabelRu = "Испытание", Category = "Combat" },
        new() { Tooltip = "MovementSpeed", Label = "Movement Speed", LabelRu = "Скорость", Category = "Combat" },
        new() { Tooltip = "OpportunityAttack", Label = "Opportunity Attack", LabelRu = "Провоцированная атака", Category = "Combat" },

        // Mechanics
        new() { Tooltip = "Advantage", Label = "Advantage", LabelRu = "Преимущество", Category = "Mechanics" },
        new() { Tooltip = "Disadvantage", Label = "Disadvantage", LabelRu = "Помеха", Category = "Mechanics" },
        new() { Tooltip = "Concentration", Label = "Concentration", LabelRu = "Концентрация", Category = "Mechanics" },
        new() { Tooltip = "ProficiencyBonus", Label = "Proficiency Bonus", LabelRu = "Бонус мастерства", Category = "Mechanics" },
        new() { Tooltip = "Immune", Label = "Immune", LabelRu = "Иммунитет", Category = "Mechanics" },
        new() { Tooltip = "Resistant", Label = "Resistant", LabelRu = "Сопротивление", Category = "Mechanics" },
        new() { Tooltip = "Vulnerable", Label = "Vulnerable", LabelRu = "Уязвимость", Category = "Mechanics" },

        // Actions
        new() { Tooltip = "Action", Label = "Action", LabelRu = "Действие", Category = "Actions" },
        new() { Tooltip = "BonusAction", Label = "Bonus Action", LabelRu = "Бонусное действие", Category = "Actions" },
        new() { Tooltip = "ShortRest", Label = "Short Rest", LabelRu = "Короткий отдых", Category = "Actions" },
        new() { Tooltip = "LongRest", Label = "Long Rest", LabelRu = "Длинный отдых", Category = "Actions" },

        // Skills
        new() { Tooltip = "Acrobatics", Label = "Acrobatics", LabelRu = "Акробатика", Category = "Skills" },
        new() { Tooltip = "Athletics", Label = "Athletics", LabelRu = "Атлетика", Category = "Skills" },
        new() { Tooltip = "Perception", Label = "Perception", LabelRu = "Восприятие", Category = "Skills" },
        new() { Tooltip = "Stealth", Label = "Stealth", LabelRu = "Скрытность", Category = "Skills" },
        new() { Tooltip = "Persuasion", Label = "Persuasion", LabelRu = "Убеждение", Category = "Skills" },
        new() { Tooltip = "Intimidation", Label = "Intimidation", LabelRu = "Запугивание", Category = "Skills" },
        new() { Tooltip = "Deception", Label = "Deception", LabelRu = "Обман", Category = "Skills" },

        // Spell-related
        new() { Tooltip = "SpellSlot", Label = "Spell Slot", LabelRu = "Ячейка заклинания", Category = "Spells" },
        new() { Tooltip = "Cantrip", Label = "Cantrip", LabelRu = "Заговор", Category = "Spells" },
        new() { Tooltip = "SpellDifficultyClass", Label = "Spell DC", LabelRu = "Сл. заклинания", Category = "Spells" },

        // Equipment
        new() { Tooltip = "Finesse", Label = "Finesse", LabelRu = "Фехтовальное", Category = "Equipment" },
        new() { Tooltip = "Versatile", Label = "Versatile", LabelRu = "Универсальное", Category = "Equipment" },
        new() { Tooltip = "TwoHanded", Label = "Two-Handed", LabelRu = "Двуручное", Category = "Equipment" },
        new() { Tooltip = "Thrown", Label = "Thrown", LabelRu = "Метательное", Category = "Equipment" },
        new() { Tooltip = "Reach", Label = "Reach", LabelRu = "Досягаемость", Category = "Equipment" },
        new() { Tooltip = "Light", Label = "Light", LabelRu = "Лёгкое", Category = "Equipment" },
    ];

    /// <summary>Common action resources for [resource=X] tags.</summary>
    public static readonly TagInfo[] ActionResources =
    [
        new() { Tooltip = "Rage", Type = "ActionResource", Label = "Rage", LabelRu = "Ярость", Category = "Resources" },
        new() { Tooltip = "KiPoint", Type = "ActionResource", Label = "Ki Point", LabelRu = "Очко ки", Category = "Resources" },
        new() { Tooltip = "SorceryPoint", Type = "ActionResource", Label = "Sorcery Point", LabelRu = "Чародейство", Category = "Resources" },
        new() { Tooltip = "BardicInspiration", Type = "ActionResource", Label = "Bardic Inspiration", LabelRu = "Бард. вдохновение", Category = "Resources" },
        new() { Tooltip = "SuperiorityDie", Type = "ActionResource", Label = "Superiority Die", LabelRu = "Кость превосходства", Category = "Resources" },
        new() { Tooltip = "ChannelDivinity", Type = "ActionResource", Label = "Channel Divinity", LabelRu = "Божеств. канал", Category = "Resources" },
        new() { Tooltip = "ChannelOath", Type = "ActionResource", Label = "Channel Oath", LabelRu = "Канал клятвы", Category = "Resources" },
        new() { Tooltip = "WildShape", Type = "ActionResource", Label = "Wild Shape", LabelRu = "Дикий облик", Category = "Resources" },
        new() { Tooltip = "LayOnHandsCharge", Type = "ActionResource", Label = "Lay on Hands", LabelRu = "Длань исцеления", Category = "Resources" },
        new() { Tooltip = "SpellSlot", Type = "ActionResource", Label = "Spell Slot", LabelRu = "Ячейка заклинания", Category = "Resources" },
        new() { Tooltip = "WarlockSpellSlot", Type = "ActionResource", Label = "Warlock Slot", LabelRu = "Ячейка колдуна", Category = "Resources" },
        new() { Tooltip = "BonusActionPoint", Type = "ActionResource", Label = "Bonus Action", LabelRu = "Бонусное действие", Category = "Resources" },
        new() { Tooltip = "ReactionActionPoint", Type = "ActionResource", Label = "Reaction", LabelRu = "Реакция", Category = "Resources" },
    ];

    /// <summary>Damage types for DescriptionParams — NOT LSTag! Used via DealDamage().</summary>
    public static readonly DamageTypeInfo[] DamageTypes =
    [
        new() { Id = "Acid", Label = "Acid", LabelRu = "Кислотный", Color = "#56E156" },
        new() { Id = "Bludgeoning", Label = "Bludgeoning", LabelRu = "Дробящий", Color = "#A0A0A0" },
        new() { Id = "Cold", Label = "Cold", LabelRu = "Холодный", Color = "#87CEEB" },
        new() { Id = "Fire", Label = "Fire", LabelRu = "Огненный", Color = "#FF6347" },
        new() { Id = "Force", Label = "Force", LabelRu = "Силовой", Color = "#E066FF" },
        new() { Id = "Lightning", Label = "Lightning", LabelRu = "Электрический", Color = "#4FC3F7" },
        new() { Id = "Necrotic", Label = "Necrotic", LabelRu = "Некротический", Color = "#7CFC00" },
        new() { Id = "Piercing", Label = "Piercing", LabelRu = "Колющий", Color = "#C0C0C0" },
        new() { Id = "Poison", Label = "Poison", LabelRu = "Ядовитый", Color = "#ADFF2F" },
        new() { Id = "Psychic", Label = "Psychic", LabelRu = "Психический", Color = "#DA70D6" },
        new() { Id = "Radiant", Label = "Radiant", LabelRu = "Излучение", Color = "#FFD700" },
        new() { Id = "Slashing", Label = "Slashing", LabelRu = "Рубящий", Color = "#B0B0B0" },
        new() { Id = "Thunder", Label = "Thunder", LabelRu = "Громовой", Color = "#F0E68C" },
    ];

    /// <summary>All categories for toolbar grouping.</summary>
    public static string[] Categories => ["Abilities", "Combat", "Mechanics", "Actions", "Skills", "Spells", "Equipment", "Resources"];

    /// <summary>Get all tooltip tags for a given category.</summary>
    public static TagInfo[] GetByCategory(string category)
    {
        var result = Tooltips.Where(t => t.Category == category).ToList();
        result.AddRange(ActionResources.Where(t => t.Category == category));
        return result.ToArray();
    }
}

public sealed class DamageTypeInfo
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public string? LabelRu { get; init; }
    public required string Color { get; init; }
}
