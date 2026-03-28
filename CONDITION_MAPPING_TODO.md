# Задание: Интеграция ConditionLabels + FunctorLabels в UI

## Что уже сделано

### 1. ConditionLabels.cs (`ParaTool.Core/Schema/ConditionLabels.cs`)
- **~400 условий** (Functions) — EN + RU лейблы
- **~40 поверхностей** (Surfaces) — EN + RU лейблы
- **14 категорий** (Categories) — EN + RU лейблы
- Хелперы: `GetLabel()`, `GetSurfaceLabel()`, `GetCategoryLabel()`

### 2. FunctorLabels.cs (`ParaTool.Core/Schema/FunctorLabels.cs`)
- **60 функторов** (Functors) — EN + RU лейблы (DealDamage, ApplyStatus, Summon, Force, ...)
- **100+ бустов** (Boosts) — EN + RU лейблы (AC, Resistance, Advantage, UnlockSpell, ...)
- **37 enum-словарей** с EN + RU:
  - AbilityLabels (6), DamageTypeLabels (22 вкл. WeaponDamageType), ResistanceLabels (11)
  - RollTypeLabels (20 вкл. Damage variants), AttackTypeLabels (9), AdvantageContextLabels (11)
  - SkillLabels (18), WeaponPropertyLabels (22), ArmorTypeLabels (14), SlotLabels (21 вкл. Wings/Horns)
  - SpellSchoolLabels (9), ProficiencyLabels (42), ActionResourceLabels (18)
  - SurfaceTypeLabels (18), SurfaceChangeLabels (11), SurfaceLayerLabels (2)
  - ResurrectTypeLabels (4), DeathTypeLabels (13), CooldownLabels (8)
  - DurationChangeLabels (4), ForceOriginLabels (3), ForceAggressionLabels (3)
  - WeaponHandLabels (3), UnlockSpellTypeLabels (3), SummonDurationLabels (2)
  - CriticalHitTypeLabels (2), CriticalHitWhenLabels (3), RollAdjustmentLabels (2)
  - DamageReductionLabels (3), HealingDirectionLabels (2), MovementSpeedLabels (4)
  - ZoneShapeLabels (2), AttributeFlagLabels (12), MagicalLabels (2), NonlethalLabels (2)
  - SizeLabels (6), TriggerEventLabels (23), ScopeLabels (4)
- Хелперы: `GetFunctorLabel()`, `GetBoostLabel()`, `GetEnumLabel()` (ищет по всем словарям)

### 3. BoostMapping.cs — расширено
- Добавлены 17 недостающих функторов: Spawn, SpawnInInventory, RemoveStatusByLevel, SurfaceClearLayer, RemoveAuraByChildStatus, CreateWall, SwitchDeathType, RegainTemporaryHitPoints, DisarmAndStealWeapon, Unlock, Sabotage, Pickup, Drop, ResetCombatTurn, SpawnExtraProjectiles, CameraWait, TutorialEvent
- Расширены enum'ы: DamageTypesExtended (+4), StatsRollType (+8 damage variants), StatItemSlot (+6 slots)
- Добавлены новые enum'ы: SurfaceLayers, DeathTypes, ResurrectTypes, SummonDurations, MagicalFlags, NonlethalFlags, SizeCategories, EngineStatusTypes, StatusRemoveCause, ObscuredState

## Что нужно сделать

### 1. ConditionBlocksEditor — показывать лейблы вместо сырых имён

**Файл:** `ParaTool.App/Controls/ConditionBlocksEditor.cs`

В методе `BuildConditionChip()` (строка ~129):
```csharp
// БЫЛО:
var label = def?.Name ?? token.FuncName;

// НАДО:
var isRu = /* определить текущий язык */;
var label = ConditionLabels.GetLabel(token.FuncName, isRu);
```

### 2. BoostBlocksEditor — показывать лейблы функторов и enum'ов

**Файл:** `ParaTool.App/Controls/BoostBlocksEditor.cs`

- Имена бустов/функторов: `FunctorLabels.GetBoostLabel(name, isRu)` или `FunctorLabels.GetFunctorLabel(name, isRu)`
- Значения ComboBox enum'ов: `FunctorLabels.GetEnumLabel(value, isRu)` — универсальный поиск по всем словарям
- Или для конкретного словаря: `FunctorLabels.GetEnumLabel(value, FunctorLabels.DamageTypeLabels, isRu)`

### 3. ConditionBlocksEditor — лейблы поверхностей в InSurface ComboBox

Показывать `ConditionLabels.GetSurfaceLabel()` вместо сырого `SurfaceWater`.

### 4. Группированное меню "Добавить условие"

Сгруппировать по категориям: `ConditionLabels.GetCategoryLabel()` как заголовки.

### 5. Удалить старый `BoostMapping.Conditions` словарь (17 записей)

Заменён `ConditionLabels.Functions` (400+ записей). Удалить или `[Obsolete]`.

### 6. Удалить старый `BoostMapping.TriggerEvents` и `BoostMapping.Scopes`

Заменены `FunctorLabels.TriggerEventLabels` и `FunctorLabels.ScopeLabels` с раздельными EN/RU.

## Ключевые файлы

| Файл | Статус |
|---|---|
| `ParaTool.Core/Schema/ConditionLabels.cs` | ✅ ГОТОВ |
| `ParaTool.Core/Schema/FunctorLabels.cs` | ✅ ГОТОВ |
| `ParaTool.Core/Schema/BoostMapping.cs` | ✅ Расширен (новые функторы + enum'ы) |
| `ParaTool.App/Controls/ConditionBlocksEditor.cs` | ❌ Нужна интеграция |
| `ParaTool.App/Controls/BoostBlocksEditor.cs` | ❌ Нужна интеграция |
| `ParaTool.App/Controls/TumblerChipEditor.cs` | ❌ Опционально: display labels |

## Важные правила

1. **Язык UI**: `ConditionLabels.GetLabel(name, isRussian)` / `FunctorLabels.GetEnumLabel(value, isRu)`
2. **Тема**: Все цвета через `ThemeBrushes` (не хардкод `Color.Parse`)
3. **"Для детей"**: Никаких сырых имён — только читаемые лейблы
4. **Fallback**: Если лейбл не найден — показывать сырое имя (все методы делают это)
5. **dotnet**: `"/mnt/c/Program Files/dotnet/dotnet.exe"` (Windows exe через WSL)
