namespace ParaTool.Core.Artifacts;

/// <summary>
/// Copies an artifact into a new, independent one: nothing the two compile to may be shared —
/// not the item and creature templates, not a loca handle, not a stats entry name. Entry names of
/// the cards are left to the compiler, which names every copy after the artifact it compiles.
/// </summary>
public static class ArtifactDuplicator
{
    /// <summary>A deep copy under <paramref name="statId"/> with its own ids, templates and handles.</summary>
    public static ArtifactDefinition? Clone(ArtifactDefinition source, string statId)
    {
        ArtifactDefinition? clone;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(source);
            clone = System.Text.Json.JsonSerializer.Deserialize<ArtifactDefinition>(json);
        }
        catch { return null; }
        if (clone == null) return null;

        clone.ArtifactId = Guid.NewGuid().ToString();
        clone.TemplateUuid = Guid.NewGuid().ToString();
        clone.StatId = statId;
        // Empty handles get fresh ones on save; the text stays on the card. Sharing a handle would
        // make one artifact's text overwrite the other's in game.
        clone.DisplayNameHandle = "";
        clone.DescriptionHandle = "";
        foreach (var p in clone.Passives)
        {
            p.DisplayNameHandle = "";
            p.DescriptionHandle = "";
        }
        foreach (var s in clone.Statuses)
        {
            s.DisplayNameHandle = "";
            s.DescriptionHandle = "";
        }
        foreach (var sp in clone.Spells.SelectMany(s => s.WithVariants()))
        {
            sp.DisplayNameHandle = "";
            sp.DescriptionHandle = "";
        }
        // Creature copies get their own templates and stats names, or the two artifacts would
        // overwrite each other's creature.
        // A compile points the spells' Summon(…) at the source's creature copy; point them back at
        // the original creature, which the compile maps to this artifact's copy.
        var spells = clone.Spells.SelectMany(s => s.WithVariants()).ToList();
        foreach (var su in clone.Summons)
        {
            var sourceCopy = su.TemplateUuid;
            su.TemplateUuid = Guid.NewGuid().ToString();
            su.StatsName = "";
            su.DisplayNameHandle = "";
            if (string.IsNullOrEmpty(sourceCopy) || string.IsNullOrEmpty(su.ParentTemplateUuid)) continue;
            foreach (var sp in spells)
            {
                sp.SpellProperties = ReplaceTemplate(sp.SpellProperties, sourceCopy, su.ParentTemplateUuid)!;
                sp.SpellSuccess = ReplaceTemplate(sp.SpellSuccess, sourceCopy, su.ParentTemplateUuid);
                sp.SpellFail = ReplaceTemplate(sp.SpellFail, sourceCopy, su.ParentTemplateUuid);
            }
        }
        return clone;
    }

    private static string? ReplaceTemplate(string? functors, string from, string to) =>
        string.IsNullOrEmpty(functors) ? functors : functors.Replace(from, to, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The "duplicate" action: the same item under <c>{StatId}_Copy</c> (or <c>_Copy_2</c>, …) —
    /// a new item, so the original's tombstones don't carry over.
    /// </summary>
    public static ArtifactDefinition? Duplicate(ArtifactDefinition source, Func<string, bool> statIdTaken)
    {
        var baseStatId = source.StatId + "_Copy";
        var statId = baseStatId;
        for (int n = 2; statIdTaken(statId); n++)
            statId = $"{baseStatId}_{n}";

        var clone = Clone(source, statId);
        if (clone == null) return null;
        clone.RemovedPassives = [];
        clone.RemovedSpells = [];
        clone.RemovedStatuses = [];
        clone.RemovedBoosts = [];
        return clone;
    }
}
