using System.Text.Json;
using System.Text.Json.Serialization;
using ParaTool.Core.Artifacts;
using ParaTool.Core.Localization;
using ParaTool.Core.Parsing;
using ParaTool.Core.Schema;

namespace ParaTool.Core.Services;

/// <summary>
/// Captures a complete diagnostic snapshot of an item / artifact's state —
/// what the tool sees, what it resolves to, and why.
/// Dumps go to %LocalAppData%/ParaTool/diag/ as JSON so an external observer
/// can verify bugs without having to poke the UI.
/// </summary>
public static class ItemDiagnostics
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly string[] Langs = ["en", "ru"];

    public static string DiagDir
    {
        get
        {
            var dir = Path.Combine(ProfileService.GetStorageDir(), "diag");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Builds a snapshot dictionary describing everything the tool currently
    /// knows about the given StatId, using the provided resolver + loca services.
    /// All lookups are non-destructive — no state mutation.
    /// </summary>
    public static Dictionary<string, object?> Capture(
        string statId,
        StatsResolver? resolver = null,
        LocaService? locaService = null,
        ArtifactDefinition? artifact = null,
        string? iconAtlasKey = null,
        int? iconBitmapWidth = null,
        int? iconBitmapHeight = null,
        Models.ItemEntry? itemEntry = null,
        string[]? pakPaths = null)
    {
        var snap = new Dictionary<string, object?>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["statId"] = statId,
        };

        // ── Stats entry ───────────────────────────
        var entry = resolver?.Get(statId);
        if (entry != null)
        {
            snap["entry"] = new
            {
                name = entry.Name,
                type = entry.Type,
                usingBase = entry.Using,
                dataKeys = entry.Data.Keys.ToArray(),
                data = entry.Data,
            };
        }
        else if (resolver != null)
        {
            snap["entry"] = "NOT FOUND in resolver";
        }

        // ── ItemEntry (scanner-level data; has RootTemplate-derived handles) ──
        if (itemEntry != null)
        {
            var rtName = !string.IsNullOrEmpty(itemEntry.DisplayNameHandle) && locaService != null
                ? new { en = locaService.ResolveHandle(itemEntry.DisplayNameHandle, "en"),
                        ru = locaService.ResolveHandle(itemEntry.DisplayNameHandle, "ru") }
                : null;
            var rtDesc = !string.IsNullOrEmpty(itemEntry.DescriptionHandle) && locaService != null
                ? new { en = locaService.ResolveHandle(itemEntry.DescriptionHandle, "en"),
                        ru = locaService.ResolveHandle(itemEntry.DescriptionHandle, "ru") }
                : null;
            snap["itemEntry"] = new
            {
                statId = itemEntry.StatId,
                displayName = itemEntry.DisplayName,
                displayNameHandle = itemEntry.DisplayNameHandle,
                descriptionHandle = itemEntry.DescriptionHandle,
                iconName = itemEntry.IconName,
                locaAncestorId = itemEntry.LocaAncestorId,
                rootTemplateNameResolved = rtName,
                rootTemplateDescResolved = rtDesc,
                detectedRarity = itemEntry.DetectedRarity,
                detectedPool = itemEntry.DetectedPool,
                detectedThemes = itemEntry.DetectedThemes,
            };
        }

        // ── Per-pak template resolution probe ─────
        // For items with a known RootTemplate UUID, ask ItemNameResolver what
        // each pak returns — this surfaces scanner bugs where a handle IS
        // present in the pak's templates but we fail to extract it.
        if (resolver != null && pakPaths != null && pakPaths.Length > 0)
        {
            var rtUuid = resolver.Resolve(statId, "RootTemplate");
            if (!string.IsNullOrEmpty(rtUuid))
            {
                var uuidMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    [rtUuid] = [statId]
                };
                var perPak = new List<object>();
                foreach (var pakPath in pakPaths)
                {
                    try
                    {
                        var (names, descs, nh, dh) =
                            ItemNameResolver.ResolveFromPakFull(pakPath, uuidMap, "en");
                        names.TryGetValue(rtUuid, out var n);
                        descs.TryGetValue(rtUuid, out var d);
                        nh.TryGetValue(rtUuid, out var nameHandle);
                        dh.TryGetValue(rtUuid, out var descHandle);
                        if (n != null || nameHandle != null)
                            perPak.Add(new
                            {
                                pak = Path.GetFileName(pakPath),
                                uuid = rtUuid,
                                name = n,
                                nameHandle,
                                desc = d,
                                descHandle,
                            });
                    }
                    catch (Exception ex)
                    {
                        perPak.Add(new { pak = Path.GetFileName(pakPath), error = ex.Message });
                    }
                }
                // Also list template files per pak so we can see scanner's view
                var pakContents = new List<object>();
                foreach (var pakPath in pakPaths)
                {
                    try
                    {
                        using var pfs = File.OpenRead(pakPath);
                        var hdr = ParaTool.Core.PakReader.ReadHeader(pfs);
                        var pEntries = ParaTool.Core.PakReader.ReadFileList(pfs, hdr);
                        var templ = pEntries.Where(e =>
                            (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) ||
                             e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) &&
                            (e.Path.Contains("RootTemplate", StringComparison.OrdinalIgnoreCase) ||
                             e.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase) ||
                             e.Path.Contains("Global", StringComparison.OrdinalIgnoreCase)))
                            .Select(e => e.Path).ToArray();
                        // Which files actually contain the UUID (text OR binary Guid)?
                        var containingFiles = new List<string>();
                        var scanStats = new List<object>();
                        var guidBytes = Guid.TryParse(rtUuid, out var guid) ? guid.ToByteArray() : null;
                        var guidBytesBE = guidBytes != null ? new byte[]
                        {
                            // LSF may store UUIDs in big-endian/raw format too
                            guidBytes[3], guidBytes[2], guidBytes[1], guidBytes[0],
                            guidBytes[5], guidBytes[4],
                            guidBytes[7], guidBytes[6],
                            guidBytes[8], guidBytes[9], guidBytes[10], guidBytes[11],
                            guidBytes[12], guidBytes[13], guidBytes[14], guidBytes[15],
                        } : null;
                        foreach (var path in templ)
                        {
                            try
                            {
                                var fe = pEntries.First(e => e.Path == path);
                                var data = ParaTool.Core.PakReader.ExtractFileData(pfs, fe);
                                byte[] raw = data;
                                bool isLsf = ParaTool.Core.Parsing.LsfScanner.IsLsf(data);
                                bool decompOk = false;
                                if (isLsf)
                                {
                                    var decomp = ParaTool.Core.Parsing.LsfScanner.TryDecompressLsf(data);
                                    if (decomp != null) { raw = decomp; decompOk = true; }
                                }
                                var text = System.Text.Encoding.Latin1.GetString(raw);
                                bool textHit = text.IndexOf(rtUuid, StringComparison.OrdinalIgnoreCase) >= 0;
                                bool binHitLE = false, binHitBE = false;
                                if (guidBytes != null)
                                {
                                    for (int i = 0; i <= raw.Length - 16; i++)
                                    {
                                        bool le = true, be = true;
                                        for (int j = 0; j < 16; j++)
                                        {
                                            if (le && raw[i + j] != guidBytes[j]) le = false;
                                            if (be && raw[i + j] != guidBytesBE![j]) be = false;
                                            if (!le && !be) break;
                                        }
                                        if (le) binHitLE = true;
                                        if (be) binHitBE = true;
                                        if (binHitLE && binHitBE) break;
                                    }
                                }
                                if (textHit || binHitLE || binHitBE)
                                {
                                    var hitType = textHit ? "text" : binHitLE ? "guid-LE" : "guid-BE";
                                    containingFiles.Add($"{path} [{hitType}]");
                                }
                                // Priority: always log RootTemplates/_merged if seen
                                if (path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) && path.Contains("_merged", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Dump raw to disk for manual inspection
                                    var dumpPath = Path.Combine(DiagDir, $"raw_{Path.GetFileName(pakPath)}_RT_merged.bin");
                                    File.WriteAllBytes(dumpPath, raw);
                                    scanStats.Add(new { path, isLsf, decompOk, rawSize = raw.Length,
                                        textHit, binHitLE, binHitBE,
                                        dumpedTo = dumpPath,
                                        rawHeadHex = string.Join(" ", raw.Take(32).Select(b => b.ToString("X2")))
                                    });
                                }
                                else if (scanStats.Count < 5)
                                    scanStats.Add(new { path, isLsf, decompOk, rawSize = raw.Length });
                            }
                            catch (Exception ex) { scanStats.Add(new { path, error = ex.Message }); }
                        }
                        var uuidNamedFile = templ.FirstOrDefault(p => p.Contains(rtUuid, StringComparison.OrdinalIgnoreCase));
                        pakContents.Add(new
                        {
                            pak = Path.GetFileName(pakPath),
                            templateFileCount = templ.Length,
                            filesContainingUuid = containingFiles.ToArray(),
                            uuidNamedFileInPak = uuidNamedFile,
                            rootTemplatesFileCount = templ.Count(p => p.Contains("RootTemplate", StringComparison.OrdinalIgnoreCase)),
                            rootTemplates_mergedInPak = templ.Any(p => p.Contains("RootTemplate", StringComparison.OrdinalIgnoreCase) && p.Contains("_merged", StringComparison.OrdinalIgnoreCase)),
                            scanStats = scanStats.ToArray(),
                        });
                    }
                    catch (Exception ex)
                    {
                        pakContents.Add(new { pak = Path.GetFileName(pakPath), error = ex.Message });
                    }
                }

                snap["templateProbe"] = new
                {
                    rootTemplateUuid = rtUuid,
                    resultsPerPak = perPak,
                    hitsCount = perPak.Count,
                    pakContents,
                };
            }
        }

        // ── Using chain walk ──────────────────────
        if (resolver != null)
        {
            var chain = new List<object>();
            var cur = statId;
            int depth = 0;
            while (cur != null && depth < 25)
            {
                if (!resolver.AllEntries.TryGetValue(cur, out var e))
                {
                    chain.Add(new { statId = cur, found = false });
                    break;
                }
                chain.Add(new
                {
                    statId = e.Name,
                    found = true,
                    type = e.Type,
                    usingBase = e.Using,
                    displayNameHandle = e.Data.GetValueOrDefault("DisplayName"),
                    descriptionHandle = e.Data.GetValueOrDefault("Description"),
                });
                if (e.Using == null || e.Using.Equals(e.Name, StringComparison.OrdinalIgnoreCase))
                    break;
                cur = e.Using;
                depth++;
            }
            snap["usingChain"] = chain;
        }

        // ── Resolved stats fields (whole ResolveAll dump) ──
        if (resolver != null)
        {
            snap["resolvedFields"] = resolver.ResolveAll(statId);
        }

        // ── Name resolution trace (for each lang) ──
        if (resolver != null)
        {
            var nameTrace = new Dictionary<string, object>();
            var descTrace = new Dictionary<string, object>();
            foreach (var lang in Langs)
            {
                nameTrace[lang] = TraceNameResolution(statId, lang, resolver, locaService, artifact, isName: true);
                descTrace[lang] = TraceNameResolution(statId, lang, resolver, locaService, artifact, isName: false);
            }
            snap["nameResolution"] = nameTrace;
            snap["descriptionResolution"] = descTrace;
        }

        // ── Artifact (.art) state ─────────────────
        if (artifact != null)
        {
            snap["artifact"] = new
            {
                artifactId = artifact.ArtifactId,
                statId = artifact.StatId,
                statType = artifact.StatType,
                usingBase = artifact.UsingBase,
                rarity = artifact.Rarity,
                lootPool = artifact.LootPool,
                lootThemes = artifact.LootThemes,
                weight = artifact.Weight,
                valueOverride = artifact.ValueOverride,
                unique = artifact.Unique,
                displayNameHandle = artifact.DisplayNameHandle,
                descriptionHandle = artifact.DescriptionHandle,
                displayName = artifact.DisplayName,
                description = artifact.Description,
                boosts = artifact.Boosts,
                passivesOnEquip = artifact.PassivesOnEquip,
                statusOnEquip = artifact.StatusOnEquip,
                spellsOnEquip = artifact.SpellsOnEquip,
                atlasIconMapKey = artifact.AtlasIconMapKey,
                hasCustomPngIcon = artifact.IconMainDdsBase64 != null,
                passiveCount = artifact.Passives.Count,
                passives = artifact.Passives.Select(p => new
                {
                    name = p.Name,
                    usingBase = p.UsingBase,
                    displayNameHandle = p.DisplayNameHandle,
                    descriptionHandle = p.DescriptionHandle,
                    displayName = p.DisplayName,
                    description = p.Description,
                    boosts = p.Boosts,
                    statsFunctors = p.StatsFunctors,
                    properties = p.Properties,
                }).ToArray(),
                statuses = artifact.Statuses.Select(s => new
                {
                    name = s.Name,
                    usingBase = s.UsingBase,
                    statusType = s.StatusType,
                    boosts = s.Boosts,
                }).ToArray(),
                spells = artifact.Spells.Select(sp => new
                {
                    name = sp.Name,
                    usingBase = sp.UsingBase,
                    spellType = sp.SpellType,
                }).ToArray(),
            };
        }

        // ── Icon ─────────────────────────────────
        snap["icon"] = new
        {
            atlasKey = iconAtlasKey ?? artifact?.AtlasIconMapKey,
            bitmapLoaded = iconBitmapWidth.HasValue && iconBitmapHeight.HasValue,
            bitmapWidth = iconBitmapWidth,
            bitmapHeight = iconBitmapHeight,
            hasCustomPng = artifact?.IconMainDdsBase64 != null,
        };

        // ── Boost preview rendering ──────────────
        if (artifact != null && !string.IsNullOrEmpty(artifact.Boosts))
        {
            snap["boostsPreview"] = new
            {
                raw = artifact.Boosts,
                parsed = artifact.Boosts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                en = BoostMapping.FormatBoostsForPreview(artifact.Boosts, k => k == "_lang" ? "en" : k.StartsWith("enum.") ? k[5..] : k),
                ru = BoostMapping.FormatBoostsForPreview(artifact.Boosts, k => k == "_lang" ? "ru" : k),
            };
        }

        return snap;
    }

    /// <summary>
    /// Walks the using-chain from statId, reporting what each tier offers for
    /// localized name or description, and which tier finally wins.
    /// </summary>
    private static object TraceNameResolution(
        string statId, string lang,
        StatsResolver resolver, LocaService? locaService, ArtifactDefinition? artifact,
        bool isName)
    {
        var steps = new List<object>();

        // Tier 0: user's .art has explicit text
        if (artifact != null)
        {
            var dict = isName ? artifact.DisplayName : artifact.Description;
            var userVal = dict.GetValueOrDefault(lang);
            steps.Add(new
            {
                tier = 0,
                source = ".art user text",
                value = userVal,
                wins = !string.IsNullOrEmpty(userVal)
            });
            if (!string.IsNullOrEmpty(userVal))
                return new { lang, steps, winner = "user .art text", value = userVal };
        }

        // Tier 1: artifact's own handle (in loca paks, if patched)
        if (artifact != null)
        {
            var artHandle = isName ? artifact.DisplayNameHandle : artifact.DescriptionHandle;
            if (!string.IsNullOrEmpty(artHandle))
            {
                var v = locaService?.ResolveHandle(artHandle, lang);
                steps.Add(new { tier = 1, source = $"art handle ({artHandle})", value = v, wins = v != null });
                if (v != null)
                    return new { lang, steps, winner = "art handle", value = v };
            }
        }

        // Tier 2: walk using-chain, at each step check own handle, then vanilla TSV
        var cur = statId;
        int depth = 0;
        while (cur != null && depth < 20)
        {
            if (!resolver.AllEntries.TryGetValue(cur, out var e))
            {
                steps.Add(new { tier = 2 + depth, statId = cur, source = "not in resolver", value = (string?)null, wins = false });
                break;
            }
            // Stats handle for this node
            var handle = e.Data.GetValueOrDefault(isName ? "DisplayName" : "Description");
            if (!string.IsNullOrEmpty(handle))
            {
                var parsed = HandleGenerator.Parse(handle).handle;
                var viaHandle = locaService?.ResolveHandle(parsed, lang);
                steps.Add(new { tier = 2 + depth, statId = cur, source = $"stats handle ({parsed})", value = viaHandle, wins = viaHandle != null });
                if (viaHandle != null)
                    return new { lang, steps, winner = $"stats handle at {cur}", value = viaHandle };
            }
            // Vanilla TSV for this node
            var viaVanilla = isName ? VanillaLocaService.GetDisplayName(cur, lang) : VanillaLocaService.GetDescription(cur, lang);
            if (viaVanilla != null)
            {
                steps.Add(new { tier = 2 + depth, statId = cur, source = "vanilla TSV", value = viaVanilla, wins = true });
                return new { lang, steps, winner = $"vanilla TSV at {cur}", value = viaVanilla };
            }
            else
            {
                steps.Add(new { tier = 2 + depth, statId = cur, source = "vanilla TSV", value = (string?)null, wins = false });
            }
            if (e.Using == null || e.Using.Equals(e.Name, StringComparison.OrdinalIgnoreCase)) break;
            cur = e.Using;
            depth++;
        }

        return new { lang, steps, winner = "NONE", value = (string?)null };
    }

    /// <summary>Write a capture snapshot to %LocalAppData%/ParaTool/diag/{statId}.json</summary>
    public static string Dump(
        string statId,
        StatsResolver? resolver = null,
        LocaService? locaService = null,
        ArtifactDefinition? artifact = null,
        string? iconAtlasKey = null,
        int? iconW = null, int? iconH = null,
        string? suffix = null,
        Models.ItemEntry? itemEntry = null,
        string[]? pakPaths = null)
    {
        var snap = Capture(statId, resolver, locaService, artifact, iconAtlasKey, iconW, iconH, itemEntry, pakPaths);
        var safeId = SanitizeFilename(statId);
        var filename = string.IsNullOrEmpty(suffix) ? $"{safeId}.json" : $"{safeId}_{suffix}.json";
        var path = Path.Combine(DiagDir, filename);
        File.WriteAllText(path, JsonSerializer.Serialize(snap, JsonOpts));
        return path;
    }

    private static string SanitizeFilename(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(s.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c));
    }
}
