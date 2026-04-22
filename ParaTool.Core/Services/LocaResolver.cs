using ParaTool.Core.Artifacts;
using ParaTool.Core.Localization;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Services;

/// <summary>
/// Single source of truth for resolving a BG3 item's display name / description.
///
/// IMPORTANT: in BG3 stats text, Armor / Weapon entries do NOT hold loca handles
/// at all — localisation for items lives in their RootTemplate (LSF). The scanner
/// extracts that template-level handle into ItemEntry.DisplayNameHandle. Stats
/// handles (DisplayName / Description fields) only exist on PassiveData,
/// StatusData, SpellData — not on items.
///
/// Priority order for items (highest wins):
///   0. User's .art DisplayName[lang] override — their local edit always wins
///   1. User art's own handle resolves in loca paks (they patched before)
///   2. Template handle from scanner (ItemEntry.DisplayNameHandle) — this is
///      the handle declared on the item's RootTemplate (mod or vanilla)
///   3. Walk using-chain of stats entries; at each ancestor try embedded
///      vanilla TSV lookup (which is pre-indexed by StatId from vanilla
///      templates). Nearest ancestor with a vanilla name wins.
///   4. null — nothing found
///
/// Why walk the stats using-chain for vanilla TSV lookup? Because a mod item
/// without its own template DisplayName inherits the parent's template through
/// the `using` chain, and vanilla_items_loca.tsv is keyed by StatId — so
/// climbing the stats chain eventually lands on a vanilla StatId whose TSV
/// entry is the inherited display name.
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

    public enum Source { UserArt, ArtHandle, TemplateHandle, VanillaTsv, NotFound }

    public record Result(string? Value, Source Source, string? MatchedAt, int Depth)
    {
        public bool Resolved => Value != null;
    }

    /// <summary>Resolve display name for an item StatId.</summary>
    /// <param name="templateHandle">RootTemplate-level DisplayName handle from scanner.</param>
    public Result ResolveName(string statId, string lang,
        ArtifactDefinition? userArt = null, string? templateHandle = null)
        => Resolve(statId, lang, userArt, templateHandle, isName: true);

    /// <summary>Resolve description for an item StatId.</summary>
    /// <param name="templateHandle">RootTemplate-level Description handle from scanner.</param>
    public Result ResolveDescription(string statId, string lang,
        ArtifactDefinition? userArt = null, string? templateHandle = null)
        => Resolve(statId, lang, userArt, templateHandle, isName: false);

    private Result Resolve(string statId, string lang,
        ArtifactDefinition? userArt, string? templateHandle, bool isName)
    {
        // Tier 0: user's .art typed text
        if (userArt != null)
        {
            var dict = isName ? userArt.DisplayName : userArt.Description;
            var userVal = dict.GetValueOrDefault(lang);
            if (!string.IsNullOrEmpty(userVal))
                return new Result(userVal, Source.UserArt, userArt.StatId, 0);
        }

        // Tier 1: user's own handle (patched previously — stored in .art)
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

        // Tier 2: RootTemplate handle from scanner (caller's responsibility to
        // pass the right handle — DisplayName or Description depending on isName).
        if (!string.IsNullOrEmpty(templateHandle) && _loca != null)
        {
            var text = _loca.ResolveHandle(templateHandle, lang);
            if (text != null)
                return new Result(BbCode.FromBg3Xml(text), Source.TemplateHandle, statId, 0);
        }

        // Tier 3: walk stats using-chain, try vanilla TSV at each tier
        var cur = statId;
        int depth = 0;
        const int maxDepth = 20;
        while (cur != null && depth < maxDepth)
        {
            var vanilla = isName
                ? VanillaLocaService.GetDisplayName(cur, lang)
                : VanillaLocaService.GetDescription(cur, lang);
            if (vanilla != null)
                return new Result(BbCode.FromBg3Xml(vanilla), Source.VanillaTsv, cur, depth);

            if (!_stats.AllEntries.TryGetValue(cur, out var entry))
                break;
            if (entry.Using == null || entry.Using.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                break;
            cur = entry.Using;
            depth++;
        }

        return new Result(null, Source.NotFound, null, depth);
    }
}
