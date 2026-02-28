# ParaTool — Ancient Mega Pack Integrator

Standalone desktop app that integrates items from third-party BG3 mods into [Ancient Mega Pack](https://www.nexusmods.com/baldursgate3/mods/2285) loot lists. Patches loot lists in-place so modded items appear in AMP's randomized loot pools alongside vanilla AMP gear — no manual editing, no broken distributions.

## Why

AMP uses `subtable "-1"` for pool-based random loot. Adding items via CanMerge creates a separate subtable, which means the game rolls twice — once from AMP's pool, once from your patch. This breaks the balance. Manual patching of TreasureTable.txt works but is tedious and breaks every AMP update.

ParaTool does it automatically: scans your mods, resolves stats through inheritance chains, and injects items directly into the correct pool positions inside AMP's existing loot lists.

## Features

- **Auto-detection** — finds BG3 Mods folder automatically (Windows/Linux)
- **Full mod scanning** — reads .pak files, parses stats, resolves inheritance chains (Armor.txt, Weapon.txt)
- **4-layer loot integration:**
  - Type pool (`REL_[Rarity]_[Slot]`) — random pool by slot + rarity
  - All-rarity pool (`REL_All_[Rarity]`) — global rarity pool
  - Theme pool (`REL_[Rarity]_[Theme]`) — thematic synergy pools
  - Debug pools (`AMP_Para_[N]`) — guaranteed drops by type/theme (for cheat ring testing)
- **In-place insertion** — items are injected INTO existing loot lists at correct positions, not appended
- **Stats overrides** — generates `ParaTool_Overrides.txt` with correct ValueOverride pricing and Unique flags
- **Dependency management** — patches AMP's `meta.lsx` to add integrated mods as dependencies
- **13 languages** — auto-detected from system locale (RU, EN, DE, FR, ES, PT, IT, PL, ZH, JA, KO, TR, UK)
- **Single-file executable** — self-contained, no .NET runtime required

## Download

Grab the latest release for your platform from [Releases](../../releases):

| Platform | File |
|----------|------|
| Windows x64 | `ParaTool-win-x64.zip` |
| Windows x86 | `ParaTool-win-x86.zip` |
| Linux x64 | `ParaTool-linux-x64.tar.gz` |

## Usage

1. Launch `ParaTool.App.exe` (or `ParaTool.App` on Linux)
2. App auto-detects your Mods folder and scans all .pak files for equipment
3. Browse items by mod — each item shows detected slot and rarity
4. Adjust slot, rarity, and thematic synergy as needed
5. Click **PATCH** — ParaTool extracts AMP .pak, patches in-place, repacks

Re-run after any AMP update to re-apply your integration.

## Building from Source

Requires [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet restore ParaTool.sln
dotnet test ParaTool.Tests/ParaTool.Tests.csproj
dotnet publish ParaTool.App/ParaTool.App.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## Architecture

```
ParaTool.Core/          — Library: pak I/O, stats parsing, patching engine
  Models/               — ModInfo, ItemEntry, FileEntry, LspkHeader
  Parsing/              — StatsParser, StatsResolver, MetaLsxParser
  Patching/             — TreasureTablePatcher, StatsOverrideGenerator, MetaLsxPatcher, AmpPatcher
  Services/             — ModScanner, ModsFolderDetector, VanillaDatabase, TempDirectoryManager
  Resources/Vanilla/    — Embedded Armor.txt, Armor_2.txt, Weapon.txt (vanilla stat DB)
  PakReader.cs          — LSPK v18 format reader
  PakWriter.cs          — LSPK v18 format writer

ParaTool.App/           — Avalonia 11 UI (MVVM, CommunityToolkit.Mvvm)
  ViewModels/           — MainWindowVM → StartupVM → ScanningVM → ItemEditorVM
  Views/                — AXAML views with compiled bindings
  Localization/langs/   — JSON locale files (auto-discovered at runtime)

ParaTool.Tests/         — xUnit tests (stats parsing, resolution, patching, pricing)
```

### Patching Pipeline

1. Extract AMP .pak to temp directory
2. Parse TreasureTable.txt — build loot list index (name — line range)
3. For each enabled item: insert `object category` lines into correct pool lists (subtable "-1"), append `new subtable "1,1"` blocks to debug pools
4. Generate `ParaTool_Overrides.txt` with ValueOverride pricing per slot/rarity
5. Patch `meta.lsx` — add ModuleShortDesc for each integrated mod as dependency
6. Repack into .pak via PakWriter

### Slot Detection

Stats — Slot mapping via resolved `Slot` + `ArmorType` + `Shield` properties:

| Resolved Slot | Condition | Pool |
|---|---|---|
| Breast | ArmorType = None/Cloth | Clothes |
| Breast | ArmorType = other | Armor |
| Helmet | — | Hats |
| Cloak | — | Cloaks |
| Gloves / Boots / Amulet / Ring | — | Gloves / Boots / Amulets / Rings |
| Melee/Ranged Main Weapon | — | Weapons + Weapons_1H or 2H |
| Shield = Yes | — | Shields |

### Pricing Grid (ValueOverride by Slot + Rarity)

| Slot | Uncommon | Rare | VeryRare | Legendary |
|---|---|---|---|---|
| Ring, Gloves, Boots, Cloak | 150 | 400 | 800 | 2500 |
| Armor, Clothes, Amulet | 200 | 500 | 1000 | 3000 |
| Weapon | 250 | 550 | 1100 | 3300 |
| Shield | 250 | 550 | 1100 | 3100 |
| Hat | 300 | 600 | 1200 | 3500 |

## Adding Languages

Create a JSON file in `ParaTool.App/Localization/langs/` named `{code}.json` (e.g. `th.json`). Copy `en.json` as template, translate all values, set `"_name"` to the display name. The file is auto-discovered at build time.

## License

MIT
