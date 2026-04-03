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
    public static CompileResult Compile(ArtifactDefinition art, bool isOverride = false)
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
        if (!isOverride && !string.IsNullOrEmpty(art.TemplateUuid))
            stats.AppendLine($"data \"RootTemplate\" \"{art.TemplateUuid}\"");
        stats.AppendLine($"data \"Rarity\" \"{art.Rarity}\"");
        // Ensure price matches rarity via PricingGrid (final safety net)
        var pool = art.LootPool ?? "Armor";
        var priceCat = Models.PricingGrid.GetSlotCategory(pool);
        var correctPrice = Models.PricingGrid.GetPrice(priceCat, art.Rarity);
        var price = art.ValueOverride > 0 ? art.ValueOverride : correctPrice;
        // If price looks like a default/stale value, use grid price
        if (price <= 200 && art.Rarity is "Rare" or "VeryRare" or "Legendary")
            price = correctPrice;
        stats.AppendLine($"data \"ValueOverride\" \"{price}\"");

        if (art.Unique)
            stats.AppendLine("data \"Unique\" \"1\"");
        if (!string.IsNullOrEmpty(art.ComboCategory))
            stats.AppendLine($"data \"ComboCategory\" \"{art.ComboCategory}\"");
        if (art.Weight >= 0)
            stats.AppendLine($"data \"Weight\" \"{art.Weight.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"");

        // Armor-specific — skip inherited defaults to not break slot/using chain
        if (art.ArmorClass >= 0)
            stats.AppendLine($"data \"ArmorClass\" \"{art.ArmorClass}\"");
        if (!string.IsNullOrEmpty(art.ArmorType) && art.ArmorType != "None")
            stats.AppendLine($"data \"ArmorType\" \"{art.ArmorType}\"");
        if (!string.IsNullOrEmpty(art.ProficiencyGroup) && art.ProficiencyGroup != "None")
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

        // Mechanics — merge Boosts + SpellsOnEquip into single "Boosts" line
        var allBoosts = new List<string>();
        if (!string.IsNullOrEmpty(art.Boosts))
            allBoosts.Add(art.Boosts);
        if (!string.IsNullOrEmpty(art.SpellsOnEquip))
        {
            foreach (var spell in art.SpellsOnEquip.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                allBoosts.Add($"UnlockSpell({spell})");
        }
        if (allBoosts.Count > 0)
        {
            var boostsStr = string.Join(";", allBoosts);
            stats.AppendLine($"data \"Boosts\" \"{boostsStr}\"");
        }
        if (!string.IsNullOrEmpty(art.BoostsOnEquipMainHand))
            stats.AppendLine($"data \"BoostsOnEquipMainHand\" \"{art.BoostsOnEquipMainHand}\"");
        if (!string.IsNullOrEmpty(art.BoostsOnEquipOffHand))
            stats.AppendLine($"data \"BoostsOnEquipOffHand\" \"{art.BoostsOnEquipOffHand}\"");
        // Build PassivesOnEquip: merge explicit list + all passives from Passives array
        var poeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(art.PassivesOnEquip))
            foreach (var n in art.PassivesOnEquip.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                poeNames.Add(n);
        foreach (var p in art.Passives ?? [])
            if (!string.IsNullOrEmpty(p.Name))
                poeNames.Add(p.Name);
        if (poeNames.Count > 0)
            stats.AppendLine($"data \"PassivesOnEquip\" \"{string.Join(";", poeNames)}\"");
        if (!string.IsNullOrEmpty(art.StatusOnEquip))
            stats.AppendLine($"data \"StatusOnEquip\" \"{art.StatusOnEquip}\"");

        stats.AppendLine();

        // ─── Passive Definitions ────────────────────────
        // Rename passives to avoid overwriting originals
        var passiveRenames = new Dictionary<string, string>();
        for (int pi = 0; pi < art.Passives.Count; pi++)
        {
            var p = art.Passives[pi];
            var originalName = p.Name;
            // If passive name doesn't start with artifact StatId, it's inherited — rename
            if (!originalName.StartsWith(art.StatId, StringComparison.OrdinalIgnoreCase)
                && p.UsingBase == null)
            {
                var newName = $"{art.StatId}_Passive_{pi + 1}";
                passiveRenames[originalName] = newName;
                p.UsingBase = originalName; // inherit from original
                p.Name = newName;
            }
        }

        // Update PassivesOnEquip with renamed passives
        if (passiveRenames.Count > 0 && !string.IsNullOrEmpty(art.PassivesOnEquip))
        {
            var renamedPoe = art.PassivesOnEquip.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var updated = renamedPoe.Select(n => passiveRenames.TryGetValue(n, out var renamed) ? renamed : n);
            art.PassivesOnEquip = string.Join(";", updated);
        }

        // Re-build stats header with updated PassivesOnEquip
        if (passiveRenames.Count > 0)
        {
            var statsText = stats.ToString();
            foreach (var (oldName, newName) in passiveRenames)
                statsText = statsText.Replace($"\"{oldName}\"", $"\"{newName}\"");
            stats.Clear();
            stats.Append(statsText);
        }

        foreach (var passive in art.Passives)
        {
            stats.AppendLine($"new entry \"{passive.Name}\"");
            stats.AppendLine("type \"PassiveData\"");
            var hasUsing = passive.UsingBase != null;
            if (hasUsing)
                stats.AppendLine($"using \"{passive.UsingBase}\"");

            // DisplayName/Description: only if handles exist
            if (!string.IsNullOrEmpty(passive.DisplayNameHandle))
                stats.AppendLine($"data \"DisplayName\" \"{HandleGenerator.FormatWithVersion(passive.DisplayNameHandle)}\"");
            if (!string.IsNullOrEmpty(passive.DescriptionHandle))
                stats.AppendLine($"data \"Description\" \"{HandleGenerator.FormatWithVersion(passive.DescriptionHandle)}\"");

            // When using a base, always write ALL fields explicitly (even empty)
            // to override inherited values. Without this, cleared fields inherit from parent.
            if (hasUsing)
            {
                stats.AppendLine($"data \"DescriptionParams\" \"{passive.DescriptionParams}\"");
                stats.AppendLine($"data \"Properties\" \"{passive.Properties}\"");
                stats.AppendLine($"data \"BoostContext\" \"{passive.BoostContext}\"");
                stats.AppendLine($"data \"BoostConditions\" \"{passive.BoostConditions}\"");
                stats.AppendLine($"data \"Boosts\" \"{passive.Boosts}\"");
                stats.AppendLine($"data \"StatsFunctorContext\" \"{passive.StatsFunctorContext}\"");
                stats.AppendLine($"data \"Conditions\" \"{passive.Conditions}\"");
                stats.AppendLine($"data \"StatsFunctors\" \"{passive.StatsFunctors}\"");
            }
            else
            {
                // New passives (no using): only write non-empty fields
                if (!string.IsNullOrEmpty(passive.DescriptionParams))
                    stats.AppendLine($"data \"DescriptionParams\" \"{passive.DescriptionParams}\"");
                if (!string.IsNullOrEmpty(passive.Properties))
                    stats.AppendLine($"data \"Properties\" \"{passive.Properties}\"");
                if (!string.IsNullOrEmpty(passive.BoostContext))
                    stats.AppendLine($"data \"BoostContext\" \"{passive.BoostContext}\"");
                if (!string.IsNullOrEmpty(passive.BoostConditions))
                    stats.AppendLine($"data \"BoostConditions\" \"{passive.BoostConditions}\"");
                if (!string.IsNullOrEmpty(passive.Boosts))
                    stats.AppendLine($"data \"Boosts\" \"{passive.Boosts}\"");
                if (!string.IsNullOrEmpty(passive.StatsFunctorContext))
                    stats.AppendLine($"data \"StatsFunctorContext\" \"{passive.StatsFunctorContext}\"");
                if (!string.IsNullOrEmpty(passive.Conditions))
                    stats.AppendLine($"data \"Conditions\" \"{passive.Conditions}\"");
                if (!string.IsNullOrEmpty(passive.StatsFunctors))
                    stats.AppendLine($"data \"StatsFunctors\" \"{passive.StatsFunctors}\"");
            }

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
            var statusHasUsing = status.UsingBase != null;
            if (statusHasUsing)
                stats.AppendLine($"using \"{status.UsingBase}\"");

            stats.AppendLine($"data \"DisplayName\" \"{HandleGenerator.FormatWithVersion(status.DisplayNameHandle)}\"");
            stats.AppendLine($"data \"Description\" \"{HandleGenerator.FormatWithVersion(status.DescriptionHandle)}\"");

            // When using a base, always write all fields to override inherited values
            if (statusHasUsing)
            {
                stats.AppendLine($"data \"DescriptionParams\" \"{status.DescriptionParams}\"");
                stats.AppendLine($"data \"Icon\" \"{status.Icon}\"");
                stats.AppendLine($"data \"StatusPropertyFlags\" \"{status.StatusPropertyFlags}\"");
                stats.AppendLine($"data \"StatusGroups\" \"{status.StatusGroups}\"");
                stats.AppendLine($"data \"StackType\" \"{status.StackType}\"");
                stats.AppendLine($"data \"StackPriority\" \"{status.StackPriority}\"");
                stats.AppendLine($"data \"Boosts\" \"{status.Boosts}\"");
                stats.AppendLine($"data \"PassivesOnApply\" \"{status.PassivesOnApply}\"");
                stats.AppendLine($"data \"RemoveEvents\" \"{status.RemoveEvents}\"");
                if (status.StatusEffect != null)
                    stats.AppendLine($"data \"StatusEffect\" \"{status.StatusEffect}\"");
            }
            else
            {
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
            }

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
            var spellHasUsing = spell.UsingBase != null;
            if (spellHasUsing)
                stats.AppendLine($"using \"{spell.UsingBase}\"");

            stats.AppendLine($"data \"DisplayName\" \"{HandleGenerator.FormatWithVersion(spell.DisplayNameHandle)}\"");
            stats.AppendLine($"data \"Description\" \"{HandleGenerator.FormatWithVersion(spell.DescriptionHandle)}\"");

            if (spellHasUsing)
            {
                stats.AppendLine($"data \"DescriptionParams\" \"{spell.DescriptionParams}\"");
                stats.AppendLine($"data \"Icon\" \"{spell.Icon}\"");
                stats.AppendLine($"data \"SpellProperties\" \"{spell.SpellProperties}\"");
                stats.AppendLine($"data \"UseCosts\" \"{spell.UseCosts}\"");
                stats.AppendLine($"data \"Cooldown\" \"{spell.Cooldown}\"");
                stats.AppendLine($"data \"SpellFlags\" \"{spell.SpellFlags}\"");
            }
            else
            {
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
            }

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
