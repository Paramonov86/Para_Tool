using System.Text;
using ParaTool.Core.Localization;

namespace ParaTool.Core.Artifacts;

/// <summary>
/// Compiles ArtifactDefinition into BG3-ready files:
/// - Stats text (Armor/Weapon + PassiveData + StatusData + SpellData)
/// - Localization XML (English + Russian)
/// - RootTemplate data for LSF generation
///
/// Output is integrated into AMP pak during patching.
/// </summary>
public static class ArtifactCompiler
{
    /// <summary>
    /// Result of compiling an artifact — all generated file contents.
    /// </summary>
    public sealed class CompileResult
    {
        /// <summary>Stats text to append to AMP stat files.</summary>
        public required string StatsText { get; init; }

        /// <summary>Localization entries per language (lang → list of (handle, xmlText)).</summary>
        public required Dictionary<string, List<(string handle, string xmlText)>> LocalizationEntries { get; init; }

        /// <summary>RootTemplate UUID and parent UUID for LSF generation.</summary>
        public required string TemplateUuid { get; init; }
        public required string ParentTemplateUuid { get; init; }

        /// <summary>Custom icon DDS files (path → data), null if using atlas icon.</summary>
        public Dictionary<string, byte[]>? IconFiles { get; init; }
    }

    /// <summary>
    /// Compile a single artifact into BG3-ready content.
    /// </summary>
    public static CompileResult Compile(ArtifactDefinition art)
    {
        var stats = new StringBuilder();
        var loca = new Dictionary<string, List<(string, string)>>
        {
            ["en"] = [],
            ["ru"] = []
        };

        // ─── Main Item Stats ────────────────────────────
        stats.AppendLine($"new entry \"{art.StatId}\"");
        stats.AppendLine($"type \"{art.StatType}\"");
        stats.AppendLine($"using \"{art.UsingBase}\"");
        stats.AppendLine($"data \"RootTemplate\" \"{art.TemplateUuid}\"");
        stats.AppendLine($"data \"Rarity\" \"{art.Rarity}\"");
        stats.AppendLine($"data \"ValueOverride\" \"{art.ValueOverride}\"");

        if (art.Unique)
            stats.AppendLine($"data \"Unique\" \"1\"");
        if (!string.IsNullOrEmpty(art.ComboCategory))
            stats.AppendLine($"data \"ComboCategory\" \"{art.ComboCategory}\"");
        if (art.Weight >= 0)
            stats.AppendLine($"data \"Weight\" \"{art.Weight}\"");

        // Armor-specific
        if (art.ArmorClass >= 0)
            stats.AppendLine($"data \"ArmorClass\" \"{art.ArmorClass}\"");
        if (art.ArmorType != null)
            stats.AppendLine($"data \"ArmorType\" \"{art.ArmorType}\"");
        if (art.ProficiencyGroup != null)
            stats.AppendLine($"data \"Proficiency Group\" \"{art.ProficiencyGroup}\"");

        // Weapon-specific
        if (art.Damage != null)
            stats.AppendLine($"data \"Damage\" \"{art.Damage}\"");
        if (art.VersatileDamage != null)
            stats.AppendLine($"data \"VersatileDamage\" \"{art.VersatileDamage}\"");
        if (art.DefaultBoosts != null)
            stats.AppendLine($"data \"DefaultBoosts\" \"{art.DefaultBoosts}\"");
        if (art.WeaponProperties != null)
            stats.AppendLine($"data \"Weapon Properties\" \"{art.WeaponProperties}\"");

        // Mechanics
        if (!string.IsNullOrEmpty(art.Boosts))
            stats.AppendLine($"data \"Boosts\" \"{art.Boosts}\"");
        if (!string.IsNullOrEmpty(art.PassivesOnEquip))
            stats.AppendLine($"data \"PassivesOnEquip\" \"{art.PassivesOnEquip}\"");
        if (!string.IsNullOrEmpty(art.StatusOnEquip))
            stats.AppendLine($"data \"StatusOnEquip\" \"{art.StatusOnEquip}\"");
        if (!string.IsNullOrEmpty(art.SpellsOnEquip))
            stats.AppendLine($"data \"Boosts\" \"UnlockSpell({art.SpellsOnEquip})\"");

        stats.AppendLine();

        // ─── Passive Definitions ────────────────────────
        foreach (var passive in art.Passives)
        {
            stats.AppendLine($"new entry \"{passive.Name}\"");
            stats.AppendLine("type \"PassiveData\"");
            if (passive.UsingBase != null)
                stats.AppendLine($"using \"{passive.UsingBase}\"");

            stats.AppendLine($"data \"DisplayName\" \"{HandleGenerator.FormatWithVersion(passive.DisplayNameHandle)}\"");
            stats.AppendLine($"data \"Description\" \"{HandleGenerator.FormatWithVersion(passive.DescriptionHandle)}\"");

            if (!string.IsNullOrEmpty(passive.DescriptionParams))
                stats.AppendLine($"data \"DescriptionParams\" \"{passive.DescriptionParams}\"");
            stats.AppendLine($"data \"Properties\" \"{passive.Properties}\"");

            if (!string.IsNullOrEmpty(passive.Icon))
                stats.AppendLine($"data \"Icon\" \"{passive.Icon}\"");

            // Boost-based
            if (!string.IsNullOrEmpty(passive.BoostContext))
                stats.AppendLine($"data \"BoostContext\" \"{passive.BoostContext}\"");
            if (!string.IsNullOrEmpty(passive.BoostConditions))
                stats.AppendLine($"data \"BoostConditions\" \"{passive.BoostConditions}\"");
            if (!string.IsNullOrEmpty(passive.Boosts))
                stats.AppendLine($"data \"Boosts\" \"{passive.Boosts}\"");

            // Functor-based
            if (!string.IsNullOrEmpty(passive.StatsFunctorContext))
                stats.AppendLine($"data \"StatsFunctorContext\" \"{passive.StatsFunctorContext}\"");
            if (!string.IsNullOrEmpty(passive.Conditions))
                stats.AppendLine($"data \"Conditions\" \"{passive.Conditions}\"");
            if (!string.IsNullOrEmpty(passive.StatsFunctors))
                stats.AppendLine($"data \"StatsFunctors\" \"{passive.StatsFunctors}\"");

            stats.AppendLine();

            AddLocaEntries(loca, passive.DisplayName, passive.DisplayNameHandle);
            AddLocaEntries(loca, passive.Description, passive.DescriptionHandle);
        }

        // ─── Status Definitions ─────────────────────────
        foreach (var status in art.Statuses)
        {
            stats.AppendLine($"new entry \"{status.Name}\"");
            stats.AppendLine("type \"StatusData\"");
            stats.AppendLine($"data \"StatusType\" \"{status.StatusType}\"");
            if (status.UsingBase != null)
                stats.AppendLine($"using \"{status.UsingBase}\"");

            stats.AppendLine($"data \"DisplayName\" \"{HandleGenerator.FormatWithVersion(status.DisplayNameHandle)}\"");
            stats.AppendLine($"data \"Description\" \"{HandleGenerator.FormatWithVersion(status.DescriptionHandle)}\"");

            if (!string.IsNullOrEmpty(status.DescriptionParams))
                stats.AppendLine($"data \"DescriptionParams\" \"{status.DescriptionParams}\"");
            if (!string.IsNullOrEmpty(status.Icon))
                stats.AppendLine($"data \"Icon\" \"{status.Icon}\"");
            if (!string.IsNullOrEmpty(status.StatusPropertyFlags))
                stats.AppendLine($"data \"StatusPropertyFlags\" \"{status.StatusPropertyFlags}\"");
            if (!string.IsNullOrEmpty(status.StatusGroups))
                stats.AppendLine($"data \"StatusGroups\" \"{status.StatusGroups}\"");
            if (!string.IsNullOrEmpty(status.StackType))
                stats.AppendLine($"data \"StackType\" \"{status.StackType}\"");
            if (status.StackPriority != 0)
                stats.AppendLine($"data \"StackPriority\" \"{status.StackPriority}\"");
            if (!string.IsNullOrEmpty(status.Boosts))
                stats.AppendLine($"data \"Boosts\" \"{status.Boosts}\"");
            if (!string.IsNullOrEmpty(status.PassivesOnApply))
                stats.AppendLine($"data \"PassivesOnApply\" \"{status.PassivesOnApply}\"");
            if (!string.IsNullOrEmpty(status.RemoveEvents))
                stats.AppendLine($"data \"RemoveEvents\" \"{status.RemoveEvents}\"");
            if (status.StatusEffect != null)
                stats.AppendLine($"data \"StatusEffect\" \"{status.StatusEffect}\"");

            stats.AppendLine();

            AddLocaEntries(loca, status.DisplayName, status.DisplayNameHandle);
            AddLocaEntries(loca, status.Description, status.DescriptionHandle);
        }

        // ─── Spell Definitions ──────────────────────────
        foreach (var spell in art.Spells)
        {
            stats.AppendLine($"new entry \"{spell.Name}\"");
            stats.AppendLine($"type \"SpellData\"");
            stats.AppendLine($"data \"SpellType\" \"{spell.SpellType}\"");
            if (spell.UsingBase != null)
                stats.AppendLine($"using \"{spell.UsingBase}\"");

            stats.AppendLine($"data \"DisplayName\" \"{HandleGenerator.FormatWithVersion(spell.DisplayNameHandle)}\"");
            stats.AppendLine($"data \"Description\" \"{HandleGenerator.FormatWithVersion(spell.DescriptionHandle)}\"");

            if (!string.IsNullOrEmpty(spell.DescriptionParams))
                stats.AppendLine($"data \"DescriptionParams\" \"{spell.DescriptionParams}\"");
            if (!string.IsNullOrEmpty(spell.Icon))
                stats.AppendLine($"data \"Icon\" \"{spell.Icon}\"");
            if (!string.IsNullOrEmpty(spell.SpellProperties))
                stats.AppendLine($"data \"SpellProperties\" \"{spell.SpellProperties}\"");
            if (!string.IsNullOrEmpty(spell.UseCosts))
                stats.AppendLine($"data \"UseCosts\" \"{spell.UseCosts}\"");
            if (!string.IsNullOrEmpty(spell.Cooldown))
                stats.AppendLine($"data \"Cooldown\" \"{spell.Cooldown}\"");
            if (!string.IsNullOrEmpty(spell.SpellFlags))
                stats.AppendLine($"data \"SpellFlags\" \"{spell.SpellFlags}\"");

            foreach (var (key, value) in spell.ExtraData)
                stats.AppendLine($"data \"{key}\" \"{value}\"");

            stats.AppendLine();

            AddLocaEntries(loca, spell.DisplayName, spell.DisplayNameHandle);
            AddLocaEntries(loca, spell.Description, spell.DescriptionHandle);
        }

        // ─── Item Localization (stored in RootTemplate, not Stats) ──
        // We still generate loca entries — the RootTemplate LSF will
        // reference these handles for DisplayName/Description.
        AddLocaEntries(loca, art.DisplayName, art.DisplayNameHandle);
        AddLocaEntries(loca, art.Description, art.DescriptionHandle);

        // ─── Icons ──────────────────────────────────────
        Dictionary<string, byte[]>? iconFiles = null;
        if (art.IconMainDdsBase64 != null && art.IconConsoleDdsBase64 != null)
        {
            iconFiles = new Dictionary<string, byte[]>
            {
                [$"GUI/Assets/Tooltips/ItemIcons/{art.StatId}.DDS"] =
                    Convert.FromBase64String(art.IconMainDdsBase64),
                [$"GUI/Assets/ControllerUIIcons/items_png/{art.StatId}.DDS"] =
                    Convert.FromBase64String(art.IconConsoleDdsBase64),
            };
        }

        return new CompileResult
        {
            StatsText = stats.ToString(),
            LocalizationEntries = loca,
            TemplateUuid = art.TemplateUuid,
            ParentTemplateUuid = art.ParentTemplateUuid,
            IconFiles = iconFiles
        };
    }

    /// <summary>
    /// Generate localization XML content for a specific language.
    /// </summary>
    public static string GenerateLocaXml(IReadOnlyList<(string handle, string xmlText)> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<contentList>");
        foreach (var (handle, text) in entries)
        {
            sb.AppendLine($"  <content contentuid=\"{handle}\" version=\"1\">{text}</content>");
        }
        sb.AppendLine("</contentList>");
        return sb.ToString();
    }

    private static void AddLocaEntries(
        Dictionary<string, List<(string, string)>> loca,
        Dictionary<string, string> texts,
        string handle)
    {
        if (string.IsNullOrEmpty(handle)) return;

        foreach (var (lang, bbcode) in texts)
        {
            if (string.IsNullOrEmpty(bbcode)) continue;

            if (!loca.ContainsKey(lang))
                loca[lang] = [];

            var xmlText = BbCode.ToBg3Xml(bbcode);
            loca[lang].Add((handle, xmlText));
        }
    }
}
