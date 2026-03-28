using System.Reflection;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Services;

public sealed class VanillaDatabase
{
    private readonly StatsResolver _resolver = new();
    private bool _loaded;

    public StatsResolver Resolver => _resolver;

    public void Load()
    {
        if (_loaded) return;

        var assembly = Assembly.GetExecutingAssembly();

        // Vanilla Armor/Weapon (load order matters — later overrides earlier)
        var armorWeaponResources = new[]
        {
            "ParaTool.Core.Resources.Vanilla.Armor.txt",
            "ParaTool.Core.Resources.Vanilla.Armor_2.txt",
            "ParaTool.Core.Resources.Vanilla.Gustav_Armor.txt",
            "ParaTool.Core.Resources.Vanilla.Weapon.txt",
            "ParaTool.Core.Resources.Vanilla.Weapon_2.txt",
            "ParaTool.Core.Resources.Vanilla.Gustav_Weapon.txt",
        };

        // Vanilla Passives/Statuses/Spells
        var extraResources = new[]
        {
            "ParaTool.Core.Resources.Vanilla.Vanilla_Passives.txt",
            "ParaTool.Core.Resources.Vanilla.Vanilla_Statuses.txt",
            "ParaTool.Core.Resources.Vanilla.Vanilla_Spells.txt",
        };

        // AMP overrides (highest priority — loaded last)
        var ampResources = new[]
        {
            "ParaTool.Core.Resources.Vanilla.AMP_Overrides.txt",
        };

        // Load in order: vanilla base → extra types → AMP overrides
        foreach (var resourceName in armorWeaponResources.Concat(extraResources).Concat(ampResources))
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) continue; // Skip missing (AMP_Overrides may not exist in tests)
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            var entries = StatsParser.Parse(text);
            _resolver.AddEntries(entries);
        }

        _loaded = true;
    }
}
