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
        var resourceNames = new[]
        {
            "ParaTool.Core.Resources.Vanilla.Armor.txt",
            "ParaTool.Core.Resources.Vanilla.Armor_2.txt",
            "ParaTool.Core.Resources.Vanilla.Weapon.txt"
        };

        foreach (var resourceName in resourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            var entries = StatsParser.Parse(text);
            _resolver.AddEntries(entries);
        }

        _loaded = true;
    }
}
