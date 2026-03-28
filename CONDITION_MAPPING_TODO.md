# Condition & Surface Localization Mapping

## What's Needed

The Condition Chips system needs human-readable labels (English + Russian) for:

### 1. Condition Functions (597 total)

Currently condition chips show raw function names like `InSurface`, `HasStatus`, `IsWeaponAttack`.
Need a mapping file: `FunctionName` -> `English Label` / `Russian Label`

**Format needed** (add to `BoostMapping.cs` or new file):
```csharp
public static readonly Dictionary<string, (string En, string Ru)> ConditionLabels = new()
{
    ["Enemy"] = ("Is Enemy", "Враг"),
    ["Ally"] = ("Is Ally", "Союзник"),
    ["Self"] = ("Is Self", "Это вы"),
    ["Combat"] = ("In Combat", "В бою"),
    ["InSurface"] = ("In Surface", "На поверхности"),
    ["HasStatus"] = ("Has Status", "Имеет статус"),
    ["IsWeaponAttack"] = ("Weapon Attack", "Атака оружием"),
    // ... etc for all commonly used conditions
};
```

**Priority conditions to label** (most used in AMP):
- Target: Enemy, Ally, Self, Party, Player, Summon
- Combat: Combat, TurnBased
- Attack: IsWeaponAttack, IsMeleeAttack, IsRangedWeaponAttack, IsSpellAttack
- Roll: IsCritical, IsMiss, IsCriticalMiss
- Status: HasStatus, StatusId
- Spell: SpellId, IsSpell, IsCantrip, IsSpellOfSchool
- Equipment: HasShieldEquipped, WieldingWeapon, WearingArmor
- Surface: InSurface
- State: Dead, IsDowned, LethalHP

### 2. Surface Types for InSurface

Currently shows raw strings like `SurfaceWater`, `SurfaceBloodElectrified`.
Need display labels:

```csharp
public static readonly Dictionary<string, (string En, string Ru)> SurfaceLabels = new()
{
    ["SurfaceWater"] = ("Water", "Вода"),
    ["SurfaceWaterElectrified"] = ("Electrified Water", "Электр. вода"),
    ["SurfaceWaterFrozen"] = ("Frozen Water", "Замёрзшая вода"),
    ["SurfaceBlood"] = ("Blood", "Кровь"),
    ["SurfaceFire"] = ("Fire", "Огонь"),
    ["SurfaceLava"] = ("Lava", "Лава"),
    ["SurfaceAcid"] = ("Acid", "Кислота"),
    ["SurfacePoison"] = ("Poison", "Яд"),
    ["SurfaceOil"] = ("Oil", "Масло"),
    ["SurfaceGrease"] = ("Grease", "Жир"),
    ["SurfaceWeb"] = ("Web", "Паутина"),
    // ... etc
};
```

### 3. Trigger Events (When)

Already have labels in `BoostMapping.TriggerEvents` — format: `"OnDamage" = "On damage dealt / При нанесении урона"`.
These work but could be split into separate En/Ru fields.

## How to Integrate

Once labels are provided:
1. Add to `BoostMapping.cs` (or new `ConditionLabels.cs`)
2. In `ConditionBlocksEditor.BuildConditionChip()` — use label instead of raw function name
3. In `TumblerChipEditor` list mode — show labels in drum, store raw values
4. In `ConditionSchema.cs` — populate `Label`/`LabelRu` fields on `ConditionDef`

## Files to Edit
- `ParaTool.Core/Schema/BoostMapping.cs` — add label dictionaries
- `ParaTool.Core/Schema/ConditionSchema.cs` — populate Label/LabelRu from dictionaries
- `ParaTool.App/Controls/ConditionBlocksEditor.cs` — use labels in chip text
- `ParaTool.App/Controls/TumblerChipEditor.cs` — support display labels vs raw values
