# Задание: Интеграция ConditionLabels в UI

## Что уже сделано

Создан файл `ParaTool.Core/Schema/ConditionLabels.cs` с полным маппингом:
- **~400 условий** (Functions) — EN + RU лейблы
- **~40 поверхностей** (Surfaces) — EN + RU лейблы
- **14 категорий** (Categories) — EN + RU лейблы
- Хелпер-методы: `GetLabel()`, `GetSurfaceLabel()`, `GetCategoryLabel()`

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

Для определения языка — `ConditionBlocksEditor` должен получать текущий язык из ViewModel или через статический сервис. Проще всего передать `bool IsRussian` через свойство контрола или привязать к `Localization.Instance.Lang`.

### 2. ConditionBlocksEditor — лейблы поверхностей в InSurface ComboBox

Когда параметр типа `enum` с `EnumValues = InSurfaceValues`, показывать `ConditionLabels.GetSurfaceLabel()` вместо сырого `SurfaceWater`.

Варианты:
- Использовать `ComboBox.ItemTemplate` с конвертером
- Или создать список `(Raw, Display)` пар и биндить через `DisplayMemberPath`

### 3. ConditionBlocksEditor — группированное меню "Добавить условие"

В методе "Add Condition" ContextMenu — сгруппировать условия по категориям из `ConditionSchema.CategorizeCondition()` и показывать `ConditionLabels.GetCategoryLabel()` как заголовки, а `ConditionLabels.GetLabel()` как пункты.

### 4. Удалить старый `BoostMapping.Conditions` словарь (17 записей)

**Файл:** `ParaTool.Core/Schema/BoostMapping.cs` (строки 319-338)

Словарь `Conditions` больше не нужен — он заменён `ConditionLabels.Functions` (400+ записей). Удалить или пометить `[Obsolete]`.

### 5. (Опционально) TumblerChipEditor — лейблы в списках

**Файл:** `ParaTool.App/Controls/TumblerChipEditor.cs`

Если TumblerChipEditor используется для выбора поверхностей или условий в режиме списка (`Items`), показывать лейблы вместо сырых значений. Для этого нужно добавить `DisplayItems` свойство или конвертер.

## Ключевые файлы

| Файл | Что делать |
|---|---|
| `ParaTool.Core/Schema/ConditionLabels.cs` | **УЖЕ ГОТОВ** — словари и хелперы |
| `ParaTool.App/Controls/ConditionBlocksEditor.cs` | Заменить `def?.Name` на `ConditionLabels.GetLabel()` |
| `ParaTool.Core/Schema/BoostMapping.cs` | Удалить/заменить старый `Conditions` словарь |
| `ParaTool.Core/Schema/ConditionSchema.cs` | Можно заполнить `Label`/`LabelRu` при загрузке из `ConditionLabels` |
| `ParaTool.App/Controls/TumblerChipEditor.cs` | Опционально: поддержка display labels |

## Важные правила

1. **Язык UI**: Приложение поддерживает русский и английский. Используй `ConditionLabels.GetLabel(name, isRussian)`.
2. **Тема**: Все цвета через `ThemeBrushes` (не хардкод `Color.Parse`).
3. **"Для детей"**: Никаких сырых имён функций — только читаемые лейблы.
4. **Fallback**: Если лейбл не найден, показывать сырое имя (метод `GetLabel` уже делает это).
5. **dotnet на этой системе**: `"/mnt/c/Program Files/dotnet/dotnet.exe"` (Windows exe через WSL).
