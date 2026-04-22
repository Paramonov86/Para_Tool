using Xunit;
using ParaTool.Core.Artifacts;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;

namespace ParaTool.Tests;

/// <summary>
/// Tests the LocaResolver priority chain. Uses a StatsResolver seeded with
/// synthetic entries and a null LocaService (so only vanilla TSV + user
/// overrides drive the resolution — which is enough to exercise priority
/// ordering and chain-walking).
/// </summary>
public class LocaResolverTests
{
    private static StatsResolver MakeStatsResolver(params (string name, string? usingBase)[] entries)
    {
        var r = new StatsResolver();
        var list = entries.Select(e => new StatsEntry
        {
            Name = e.name,
            Type = "Weapon",
            Using = e.usingBase,
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        }).ToList();
        r.AddEntries(list);
        return r;
    }

    // ── Tier 0: user's .art overrides everything ─────────────

    [Fact]
    public void UserArtText_Wins_OverEverything()
    {
        var stats = MakeStatsResolver(("MAG_Sword", null));
        var resolver = new LocaResolver(stats, null);
        var art = new ArtifactDefinition
        {
            StatId = "MAG_Sword",
            DisplayName = { ["en"] = "My Custom Sword" }
        };

        var r = resolver.ResolveName("MAG_Sword", "en", art);
        Assert.Equal("My Custom Sword", r.Value);
        Assert.Equal(LocaResolver.Source.UserArt, r.Source);
    }

    [Fact]
    public void UserArtEmpty_FallsThrough()
    {
        var stats = MakeStatsResolver(("WPN_Sword", null));
        var resolver = new LocaResolver(stats, null);
        var art = new ArtifactDefinition
        {
            StatId = "WPN_Sword",
            DisplayName = { ["en"] = "" }  // empty — should not win
        };

        // With vanilla TSV "Sword" for WPN_Sword (known embedded entry), should get that.
        var r = resolver.ResolveName("WPN_Sword", "en", art);
        // Vanilla TSV might have "Longsword" etc — we just assert it's NOT the empty user text
        Assert.NotEqual("", r.Value);
        Assert.NotEqual(LocaResolver.Source.UserArt, r.Source);
    }

    // ── Chain walking ─────────────────────────────────────────

    [Fact]
    public void ChainWalk_NearestVanillaWins()
    {
        // child → parent → grandparent; only parent has a vanilla TSV entry
        var stats = MakeStatsResolver(
            ("CUSTOM_Child", "WPN_Longsword_1"),
            ("WPN_Longsword_1", "WPN_Longsword"),
            ("WPN_Longsword", null)
        );
        var resolver = new LocaResolver(stats, null);

        var r = resolver.ResolveName("CUSTOM_Child", "en");
        Assert.NotNull(r.Value);
        Assert.Equal(LocaResolver.Source.VanillaTsv, r.Source);
        Assert.True(r.Depth > 0, "Should find via ancestor (depth > 0)");
    }

    [Fact]
    public void UnknownStatId_ReturnsNotFound()
    {
        var stats = MakeStatsResolver(("WPN_X", null));
        var resolver = new LocaResolver(stats, null);
        var r = resolver.ResolveName("DoesNotExist_Anywhere_Zzz", "en");
        Assert.Null(r.Value);
        Assert.Equal(LocaResolver.Source.NotFound, r.Source);
    }

    [Fact]
    public void SelfReferencingUsing_DoesntLoop()
    {
        var stats = new StatsResolver();
        stats.AddEntries([new StatsEntry { Name = "A", Type = "Weapon", Using = "A", Data = [] }]);
        var resolver = new LocaResolver(stats, null);
        // Should terminate quickly (no infinite loop)
        var r = resolver.ResolveName("A", "en");
        Assert.NotNull(r); // doesn't hang
    }

    // ── Description resolution follows same rules ─────────────

    [Fact]
    public void ResolveDescription_FollowsSameChain()
    {
        var stats = MakeStatsResolver(("WPN_Longsword", null));
        var resolver = new LocaResolver(stats, null);

        var name = resolver.ResolveName("WPN_Longsword", "en");
        var desc = resolver.ResolveDescription("WPN_Longsword", "en");
        // Both should resolve or both should be NotFound — same code path
        Assert.Equal(name.Source == LocaResolver.Source.NotFound,
                     desc.Source == LocaResolver.Source.NotFound);
    }

    // ── User override: non-empty string blocks, empty doesn't ─

    [Fact]
    public void UserArt_WhitespaceOnly_AlsoBlocksFallback()
    {
        var stats = MakeStatsResolver(("MAG_X", null));
        var resolver = new LocaResolver(stats, null);
        var art = new ArtifactDefinition { StatId = "MAG_X", DisplayName = { ["en"] = "   " } };

        // Current semantics: non-empty (even whitespace) wins. This may change
        // later, but guard the current behaviour explicitly.
        var r = resolver.ResolveName("MAG_X", "en", art);
        Assert.Equal("   ", r.Value);
        Assert.Equal(LocaResolver.Source.UserArt, r.Source);
    }

    [Fact]
    public void UserArt_DifferentLang_DoesntBlockOtherLang()
    {
        var stats = MakeStatsResolver(("WPN_Longsword", null));
        var resolver = new LocaResolver(stats, null);
        var art = new ArtifactDefinition
        {
            StatId = "WPN_Longsword",
            DisplayName = { ["ru"] = "Русское название" }
        };

        // User has RU text but not EN — EN should still fall through
        var en = resolver.ResolveName("WPN_Longsword", "en", art);
        Assert.NotEqual("Русское название", en.Value);

        var ru = resolver.ResolveName("WPN_Longsword", "ru", art);
        Assert.Equal("Русское название", ru.Value);
        Assert.Equal(LocaResolver.Source.UserArt, ru.Source);
    }

    [Fact]
    public void Resolved_Property_ReflectsValuePresence()
    {
        var stats = MakeStatsResolver(("MAG_X", null));
        var resolver = new LocaResolver(stats, null);
        var hit = resolver.ResolveName("WPN_Longsword", "en");
        var miss = resolver.ResolveName("Nothing_Here_ZZZZ", "en");

        if (hit.Value != null) Assert.True(hit.Resolved);
        Assert.False(miss.Resolved);
    }
}
