using System.Text.Json;

namespace ParaTool.Core.Services;

/// <summary>
/// Persists user-selected favorite condition names.
/// Stored in %LocalAppData%/ParaTool/condition_favorites.json
/// </summary>
public static class FavoritesStore
{
    private static string FilePath => Path.Combine(ProfileService.GetStorageDir(), "condition_favorites.json");

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
                if (list != null) { _cache = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase); return _cache; }
            }
        }
        catch { }
        _cache = new HashSet<string>(DefaultFavorites, StringComparer.OrdinalIgnoreCase);
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
        if (favs.Contains(name))
        {
            favs.Remove(name);
            Save();
            return false;
        }
        favs.Add(name);
        Save();
        return true;
    }

    public static bool IsFavorite(string name) => Load().Contains(name);

    public static readonly string[] DefaultFavorites =
    [
        "Enemy", "Ally", "Self", "Combat", "TurnBased",
        "HasStatus", "SpellId", "IsWeaponAttack", "IsSpellAttack", "IsMeleeAttack",
        "IsRangedWeaponAttack", "IsCritical", "IsMiss", "InSurface",
        "HasShieldEquipped", "Dead", "IsSpell", "IsCantrip", "HasPassive",
        "IsSneakAttack", "IsLeveledSpell", "AttackedWithPassiveSourceWeapon"
    ];
}
