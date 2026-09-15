using ParaTool.Core.Localization;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;

namespace ParaTool.Core.Artifacts;

/// <summary>Builds a creature card from the template a <c>Summon()</c> spawns.</summary>
public static class SummonCloner
{
    /// <summary>Character stats fields on the creature card, in display order.</summary>
    public static readonly string[] CardFields =
    [
        "Level", "Vitality", "Armor",
        "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma",
        "Passives", "DefaultBoosts",
    ];

    /// <summary>Null when the template is not a known creature.</summary>
    public static SummonDefinition? CloneFrom(string templateUuid, StatsResolver? resolver)
    {
        var entry = SummonTemplateIndex.Find(templateUuid);
        if (entry == null) return null;

        var summon = new SummonDefinition
        {
            ParentTemplateUuid = entry.TemplateUuid,
            UsingBase = entry.Stats,
            DisplayNameHandle = HandleGenerator.New(),
        };
        summon.DisplayName["en"] = entry.NameEn ?? "";
        summon.DisplayName["ru"] = entry.NameRu ?? entry.NameEn ?? "";

        if (resolver != null)
        {
            var fields = resolver.ResolveAll(entry.Stats);
            foreach (var key in CardFields)
                if (fields.TryGetValue(key, out var value))
                    summon.Stats[key] = value;
        }
        return summon;
    }
}
