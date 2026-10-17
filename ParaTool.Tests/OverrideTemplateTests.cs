using ParaTool.Core.Artifacts;
using ParaTool.Core.LSLib;
using ParaTool.Core.Patching;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// AMP tiers (AMP_EnhanceShaman_Amulet_1, _3) inherit the base item's RootTemplate through
/// `using`, so no template carries their StatId. Overriding one used to change nothing on the
/// template, and a new name or lore never showed in game.
/// </summary>
public class OverrideTemplateTests
{
    private const string BaseTemplate = "6b8db2be-3813-4cae-aa52-d875c8a76d30";
    private const string BaseStat = "AMP_Shaman_Amulet";

    private static void WriteMerged(string path)
    {
        var resource = new Resource
        {
            Metadata = new LSMetadata { MajorVersion = 4, MinorVersion = 8, Revision = 0, BuildNumber = 500 },
            MetadataFormat = LSFMetadataFormat.KeysAndAdjacency,
        };
        var region = new Region { Name = "Templates", RegionName = "Templates" };
        resource.Regions["Templates"] = region;

        var node = new Node { Name = "GameObjects", Parent = region };
        node.Attributes["MapKey"] = new NodeAttribute(AttributeType.FixedString) { Value = BaseTemplate };
        node.Attributes["Type"] = new NodeAttribute(AttributeType.FixedString) { Value = "item" };
        node.Attributes["Stats"] = new NodeAttribute(AttributeType.FixedString) { Value = BaseStat };
        node.Attributes["Description"] = new NodeAttribute(AttributeType.TranslatedString)
        {
            Value = new TranslatedString { Handle = "hOLD", Version = 1 }
        };
        region.AppendChild(node);

        using var fs = File.Create(path);
        new LSFWriter(fs).Write(resource);
    }

    private static Node ReadFirstGameObject(string path)
    {
        using var fs = File.OpenRead(path);
        var resource = new LSFReader(fs).Read();
        return resource.Regions["Templates"].Children["GameObjects"][0];
    }

    private static string DescriptionHandle(Node node) =>
        ((TranslatedString)node.Attributes["Description"].Value!).Handle;

    private static ArtifactDefinition Override(string statId) => new()
    {
        StatId = statId,
        UsingBase = statId,
        StatType = "Armor",
        ParentTemplateUuid = BaseTemplate,
        DisplayNameHandle = "hNAME",
        DescriptionHandle = "hNEW",
    };

    private static void WithRtDir(Action<string, string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "paratool-rt-" + Guid.NewGuid().ToString("N"));
        var rtDir = Path.Combine(root, "Public", "AMP", "RootTemplates");
        Directory.CreateDirectory(rtDir);
        try { body(root, rtDir); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void OverrideOfTierWithoutOwnTemplate_GetsClonedTemplateWithNewLore()
    {
        WithRtDir((root, rtDir) =>
        {
            var merged = Path.Combine(rtDir, "_merged.lsf");
            WriteMerged(merged);
            var tier = Override(BaseStat + "_1");
            tier.LookEdited = true;
            var registry = new OverrideTemplateRegistry();

            var own = AmpPatcher.PatchRootTemplates(root, [], [tier], registry: registry);

            Assert.Equal(tier.TemplateUuid, own[tier.StatId]);
            Assert.Equal(tier.TemplateUuid, registry.Find(tier.StatId)?.TemplateUuid);
            var node = ReadFirstGameObject(Path.Combine(rtDir, $"{tier.TemplateUuid}.lsf"));
            Assert.Equal(tier.StatId, node.Attributes["Stats"].Value?.ToString());
            Assert.Equal(BaseTemplate, node.Attributes["ParentTemplateId"].Value?.ToString());
            Assert.Equal("hNEW", DescriptionHandle(node));

            // The base item and its other tiers keep their own lore.
            Assert.Equal("hOLD", DescriptionHandle(ReadFirstGameObject(merged)));
        });
    }

    /// <summary>
    /// RusMage lowered a cap on a tier helmet: the item got a template of its own, his character
    /// wore an instance of it, and once the edit was gone the item vanished from his save. A tier
    /// whose name, lore and icon are untouched keeps the template it inherits.
    /// </summary>
    [Fact]
    public void OverrideOfTier_WithItsLookUntouched_KeepsTheInheritedTemplate()
    {
        WithRtDir((root, rtDir) =>
        {
            WriteMerged(Path.Combine(rtDir, "_merged.lsf"));
            var tier = Override(BaseStat + "_1");
            var registry = new OverrideTemplateRegistry();

            var own = AmpPatcher.PatchRootTemplates(root, [], [tier], registry: registry);

            Assert.Empty(own);
            Assert.Empty(registry.Entries);
            Assert.Single(Directory.GetFiles(rtDir, "*.lsf"));
        });
    }

    [Fact]
    public void OverrideOfTier_FromAnOlderBuild_KeepsGettingItsTemplate()
    {
        // Version 1 cards did not track look edits, and got a template on every patch since 0.7.7:
        // items in saves may be instances of it.
        WithRtDir((root, rtDir) =>
        {
            WriteMerged(Path.Combine(rtDir, "_merged.lsf"));
            var tier = Override(BaseStat + "_1");
            tier.FormatVersion = 1;

            var own = AmpPatcher.PatchRootTemplates(root, [], [tier], registry: new OverrideTemplateRegistry());

            Assert.Equal(tier.TemplateUuid, own[tier.StatId]);
        });
    }

    [Fact]
    public void IssuedTemplate_IsWrittenOnEveryPatch_UnderTheUuidFirstIssued()
    {
        WithRtDir((root, rtDir) =>
        {
            WriteMerged(Path.Combine(rtDir, "_merged.lsf"));
            const string issued = "11111111-2222-3333-4444-555555555555";
            var registry = new OverrideTemplateRegistry();
            registry.Register(BaseStat + "_1", "Armor", issued, BaseTemplate);
            // The same edit made again (a reinstall made a new card), look untouched.
            var tier = Override(BaseStat + "_1");

            var own = AmpPatcher.PatchRootTemplates(root, [], [tier], registry: registry);

            Assert.Equal(issued, own[tier.StatId]);
            Assert.True(File.Exists(Path.Combine(rtDir, $"{issued}.lsf")));
        });
    }

    [Fact]
    public void IssuedTemplate_OfAnEditThatIsGone_IsKeptAsThePlainItem()
    {
        WithRtDir((root, rtDir) =>
        {
            var merged = Path.Combine(rtDir, "_merged.lsf");
            WriteMerged(merged);
            const string issued = "11111111-2222-3333-4444-555555555555";
            var registry = new OverrideTemplateRegistry();
            registry.Register(BaseStat + "_1", "Armor", issued, BaseTemplate);

            var own = AmpPatcher.PatchRootTemplates(root, [], [], registry: registry);

            // Instances in saves keep a template; nothing points new drops at it.
            Assert.Empty(own);
            var kept = ReadFirstGameObject(Path.Combine(rtDir, $"{issued}.lsf"));
            Assert.Equal(BaseStat + "_1", kept.Attributes["Stats"].Value?.ToString());
            Assert.Equal(BaseTemplate, kept.Attributes["ParentTemplateId"].Value?.ToString());
            Assert.Equal("hOLD", DescriptionHandle(kept));
        });
    }

    [Fact]
    public void Registry_RoundTripsThroughItsFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "paratool-reg-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var registry = new OverrideTemplateRegistry();
            registry.Register("A_1", "Weapon", "uuid-a", "parent-a");
            registry.Register("A_1", "Weapon", "uuid-b", "parent-b");
            registry.Save(path);
            var loaded = OverrideTemplateRegistry.Load(path);
            Assert.Equal("uuid-a", Assert.Single(loaded.Entries).TemplateUuid);
            Assert.Empty(OverrideTemplateRegistry.Load(path + ".missing").Entries);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void OverrideWithOwnTemplate_UpdatesItInPlace()
    {
        WithRtDir((root, rtDir) =>
        {
            var merged = Path.Combine(rtDir, "_merged.lsf");
            WriteMerged(merged);
            var baseItem = Override(BaseStat);

            var own = AmpPatcher.PatchRootTemplates(root, [], [baseItem], registry: new OverrideTemplateRegistry());

            Assert.Empty(own);
            Assert.Equal("hNEW", DescriptionHandle(ReadFirstGameObject(merged)));
            Assert.Single(Directory.GetFiles(rtDir, "*.lsf"));
        });
    }
}
