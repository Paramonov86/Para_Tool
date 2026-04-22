using System.Text.Json;

namespace ParaTool.Core.Services;

/// <summary>
/// Persists user-selected favorite boost/functor FuncNames.
/// Stored in %LocalAppData%/ParaTool/boost_favorites.json
/// </summary>
public static class BoostFavoritesStore
{
    public enum Context { ItemBoosts, WeaponDefaultBoosts, Functors }

    private static string FilePath => Path.Combine(ProfileService.GetStorageDir(), "boost_favorites.json");

    private static HashSet<string>? _cache;

    public static HashSet<string> Load()
    {
        if (_cache != null) return _cache;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null)
                {
                    _cache = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
                    return _cache;
                }
            }
        }
        catch { }
        _cache = new HashSet<string>(AllDefaults(), StringComparer.OrdinalIgnoreCase);
        return _cache;
    }

    public static void Save()
    {
        if (_cache == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_cache.ToList()));
        }
        catch { }
    }

    public static bool Toggle(string name)
    {
        var favs = Load();
        if (favs.Contains(name)) { favs.Remove(name); Save(); return false; }
        favs.Add(name); Save(); return true;
    }

    public static bool IsFavorite(string name) => Load().Contains(name);

    public static string[] GetDefaultFavoritesFor(Context ctx) => ctx switch
    {
        Context.Functors =>
            ["ApplyStatus", "DealDamage", "RegainHitPoints", "RemoveStatus"],
        Context.WeaponDefaultBoosts =>
            ["WeaponEnchantment", "WeaponProperty", "WeaponDamage", "CharacterWeaponDamage"],
        _ =>
            ["AC", "Ability", "Advantage", "RollBonus", "DamageBonus",
             "Resistance", "UnlockSpell", "ProficiencyBonusIncrease", "CriticalHit"],
    };

    private static IEnumerable<string> AllDefaults() =>
        GetDefaultFavoritesFor(Context.ItemBoosts)
            .Concat(GetDefaultFavoritesFor(Context.WeaponDefaultBoosts))
            .Concat(GetDefaultFavoritesFor(Context.Functors));
}
