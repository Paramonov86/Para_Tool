using ParaTool.Core.Localization;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Artifacts;

/// <summary>
/// Builds a status card from an existing StatusData entry, or a blank one. Only the fields the
/// card edits are copied; visuals, sounds and animations stay inherited through <c>using</c>.
/// Text is not resolved here — the caller fills DisplayName/Description from loca.
/// </summary>
public static class StatusCloner
{
    public static StatusDefinition CloneFrom(string statusName, StatsResolver? resolver)
    {
        var (nameHandle, descHandle) = HandleGenerator.NewPair();
        var status = new StatusDefinition
        {
            Name = statusName,
            UsingBase = statusName,
            DisplayNameHandle = nameHandle,
            DescriptionHandle = descHandle,
        };
        if (resolver == null) return status;

        var f = resolver.ResolveAll(statusName);
        string? Get(string key) => f.TryGetValue(key, out var v) ? v : null;

        status.StatusType = Get("StatusType") ?? status.StatusType;
        status.DescriptionParams = Get("DescriptionParams") ?? "";
        status.Icon = Get("Icon");
        status.StatusPropertyFlags = Get("StatusPropertyFlags") ?? "";
        status.StatusGroups = Get("StatusGroups") ?? "";
        status.Boosts = Get("Boosts") ?? "";
        status.RemoveEvents = Get("RemoveEvents") ?? "";
        status.StackId = Get("StackId");
        status.StackType = Get("StackType");
        status.StackPriority = int.TryParse(Get("StackPriority"), out var priority) ? priority : null;
        status.Passives = Get("Passives");
        status.TickType = Get("TickType");
        status.TickFunctors = Get("TickFunctors");
        status.OnApplyFunctors = Get("OnApplyFunctors");
        status.OnRemoveFunctors = Get("OnRemoveFunctors");
        status.RemoveConditions = Get("RemoveConditions");
        status.AuraRadius = Get("AuraRadius");
        status.AuraStatuses = Get("AuraStatuses");

        if (Get("DisplayName") is { Length: > 0 } dn) status.SourceDisplayNameHandle = HandleGenerator.Parse(dn).handle;
        if (Get("Description") is { Length: > 0 } dd) status.SourceDescriptionHandle = HandleGenerator.Parse(dd).handle;
        return status;
    }

    /// <summary>
    /// A status of this artifact made from scratch: a BOOST with its own stack, so it neither
    /// replaces nor is replaced by any other status.
    /// </summary>
    public static StatusDefinition CreateBlank(ArtifactDefinition art)
    {
        var (nameHandle, descHandle) = HandleGenerator.NewPair();
        var name = NextName(art);
        return new StatusDefinition
        {
            Name = name,
            StatusType = "BOOST",
            StackId = name,
            StackType = "Overwrite",
            DisplayNameHandle = nameHandle,
            DescriptionHandle = descHandle,
        };
    }

    /// <summary>The first free <c>{StatId}_Status_{n}</c>.</summary>
    public static string NextName(ArtifactDefinition art, ISet<string>? taken = null)
    {
        taken ??= new HashSet<string>(art.Statuses.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        int n = 1;
        string name;
        do name = $"{art.StatId}_Status_{n++}"; while (taken.Contains(name));
        return name;
    }

    /// <summary>Card fields and the stats keys they compile to, for tests and diagnostics.</summary>
    public static readonly string[] CardFields =
    [
        "DescriptionParams", "Icon", "StackId", "StackType", "StackPriority", "StatusPropertyFlags",
        "StatusGroups", "Boosts", "Passives", "TickType", "TickFunctors", "OnApplyFunctors",
        "OnRemoveFunctors", "RemoveConditions", "RemoveEvents", "AuraRadius", "AuraStatuses",
    ];
}
