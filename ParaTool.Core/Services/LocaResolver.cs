using ParaTool.Core.Artifacts;
using ParaTool.Core.Localization;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Services;

/// <summary>
/// Single source of truth for resolving a BG3 item's display name / description
/// from the tangle of sources we know about (user's own .art override, mod loca
/// pak handles, embedded vanilla TSV, using-chain fallbacks).
///
/// Priority order (highest wins):
///   0. User's .art override (if an ArtifactDefinition is supplied and its
///      DisplayName[lang] is non-empty). Their local edit always wins.
///   1. Artifact's own handle resolves in loca paks (means they patched once
///      before and the patched text is what they're looking at).
///   2. Walk using-chain starting at statId, at each tier:
///      a. If that tier's stats entry has a Display/Description handle and
///         LocaService resolves it — use it.
///      b. Else if the embedded vanilla TSV has a name for that tier — use it.
///      (nearest ancestor wins — so a mod can override vanilla by declaring
///      its own handle on the same StatId.)
///   3. null — nothing found.
///
/// Why not vanilla-first? Because mods frequently define their own entry with
/// a StatId that also exists in vanilla (`using "vanilla_X"`). The mod's loca
/// MUST beat the vanilla copy with the same StatId, otherwise we'd silently
/// replace mod names with vanilla ones.
/// </summary>
public sealed class LocaResolver
{
    private readonly StatsResolver _stats;
    private readonly LocaService? _loca;

    public LocaResolver(StatsResolver stats, LocaService? loca)
    {
        _stats = stats;
        _loca = loca;
    }

    public enum Source { UserArt, ArtHandle, StatsHandle, VanillaTsv, NotFound }

    public record Result(string? Value, Source Source, string? MatchedAt, int Depth)
    {
        public bool Resolved => Value != null;
    }

    public Result ResolveName(string statId, string lang, ArtifactDefinition? userArt = null)
        => Resolve(statId, lang, userArt, isName: true);

    public Result ResolveDescription(string statId, string lang, ArtifactDefinition? userArt = null)
        => Resolve(statId, lang, userArt, isName: false);

    private Result Resolve(string statId, string lang, ArtifactDefinition? userArt, bool isName)
    {
        // Tier 0: user's .art has their own typed text
        if (userArt != null)
        {
            var dict = isName ? userArt.DisplayName : userArt.Description;
            var userVal = dict.GetValueOrDefault(lang);
            if (!string.IsNullOrEmpty(userVal))
                return new Result(userVal, Source.UserArt, userArt.StatId, 0);
        }

        // Tier 1: artifact's own handle resolves in loca paks
        if (userArt != null && _loca != null)
        {
            var handle = isName ? userArt.DisplayNameHandle : userArt.DescriptionHandle;
            if (!string.IsNullOrEmpty(handle))
            {
                var text = _loca.ResolveHandle(handle, lang);
                if (text != null)
                    return new Result(BbCode.FromBg3Xml(text), Source.ArtHandle, userArt.StatId, 0);
            }
        }

        // Tier 2: walk using-chain; at each node try stats handle, then vanilla TSV
        var cur = statId;
        int depth = 0;
        const int maxDepth = 20;
        while (cur != null && depth < maxDepth)
        {
            if (!_stats.AllEntries.TryGetValue(cur, out var entry))
                break;

            // 2a. Stats handle
            if (_loca != null)
            {
                var handleRef = entry.Data.GetValueOrDefault(isName ? "DisplayName" : "Description");
                if (!string.IsNullOrEmpty(handleRef))
                {
                    var parsed = HandleGenerator.Parse(handleRef).handle;
                    if (!string.IsNullOrEmpty(parsed))
                    {
                        var text = _loca.ResolveHandle(parsed, lang);
                        if (text != null)
                            return new Result(BbCode.FromBg3Xml(text), Source.StatsHandle, cur, depth);
                    }
                }
            }

            // 2b. Vanilla TSV at this tier
            var vanilla = isName
                ? VanillaLocaService.GetDisplayName(cur, lang)
                : VanillaLocaService.GetDescription(cur, lang);
            if (vanilla != null)
                return new Result(BbCode.FromBg3Xml(vanilla), Source.VanillaTsv, cur, depth);

            // Move up the chain — break on self-reference
            if (entry.Using == null || entry.Using.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                break;
            cur = entry.Using;
            depth++;
        }

        return new Result(null, Source.NotFound, null, depth);
    }
}
