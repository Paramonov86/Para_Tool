using System.Reflection;
using System.Xml.Linq;

namespace ParaTool.Core.Schema;

/// <summary>
/// Every argument the game takes for a boost or functor, in order, from LSLibDefinitions.xml.
/// The blocks in <see cref="BoostMapping"/> describe only the arguments ParaTool draws drums for;
/// DealDamage alone takes eight more (Magical, Nonlethal, IgnoreImmune…), and these must stay
/// visible and writable.
/// </summary>
public static class EngineSignatures
{
    private static readonly Lazy<(Dictionary<string, string[]> boosts, Dictionary<string, string[]> functors)> _all = new(Load);

    /// <summary>The argument names of a functor (or boost), empty when the game defines none.</summary>
    public static string[] Args(string funcName, bool functor)
    {
        var (boosts, functors) = _all.Value;
        var first = functor ? functors : boosts;
        var second = functor ? boosts : functors;
        return first.GetValueOrDefault(funcName) ?? second.GetValueOrDefault(funcName) ?? [];
    }

    private static (Dictionary<string, string[]>, Dictionary<string, string[]>) Load()
    {
        var boosts = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var functors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ParaTool.Core.Resources.Schema.LSLibDefinitions.xml");
        if (stream == null) return (boosts, functors);
        foreach (var f in XDocument.Load(stream).Descendants("Function"))
        {
            var name = (string?)f.Attribute("Name");
            if (name == null) continue;
            var args = f.Elements("Arg").Select(a => (string?)a.Attribute("Name") ?? "").ToArray();
            switch ((string?)f.Attribute("Type"))
            {
                case "Boost": boosts[name] = args; break;
                case "Functor": functors[name] = args; break;
            }
        }
        return (boosts, functors);
    }
}
