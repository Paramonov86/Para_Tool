# Конструктор Артефактов — Roadmap

## Концепция

Конструктор артефактов — вторая вкладка ParaTool (после Патчера). Позволяет игрокам создавать кастомные предметы для BG3 через GUI, без знания Divinity Engine. Артефакты интегрируются в AMP пак через единый патч.

**Целевая аудитория**: зумеры-моддеры, которые хотят создавать предметы быстро и визуально.

**Ключевой принцип**: Темплейт + Стат = одна связка. Юзер не видит разделения на RootTemplate и Stats — для него это один предмет.

## Архитектура

### Навигация
- Нижний таб-бар: `ПАТЧЕР` | `КОНСТРУКТОР` | `ПРОПАТЧИТЬ`
- При патче артефакты из конструктора компилируются и вшиваются в AMP пак вместе с обычными мод-предметами

### 3-колоночный Layout конструктора
```
┌─────────────────┬───────────────────────┬──────────────────┐
│   НАВИГАТОР     │     РЕДАКТОР          │   ПРЕВЬЮ         │
│   (250px)       │     (flex)            │   (320px)        │
│                 │                       │                  │
│ Дерево предметов│  Карточки свойств     │  Игровой тултип  │
│ AMP + моды      │  BB-code редактор     │  с иконкой       │
│ + кнопка создать│  Тулбар тегов         │  (sticky)        │
└─────────────────┴───────────────────────┴──────────────────┘
```

### Дизайн-система: Карточки + Чипы
- **Карточки** — складываемые секции: Основное, Пассивки, Статусы, Описание, Иконка, Лут
- **Чипы** — для выбора из списка (Редкость, Слот, Тип урона) — один клик
- **Теги** — пассивки/бусты как удаляемые чипы с + кнопкой

### Сценарии использования
1. **Создать с нуля**: выбрать базовый предмет → настроить свойства → патч
2. **Клонировать**: правый клик на существующий → "Создать наследника" → изменить
3. **Патчинг**: артефакты появляются в общем списке предметов → патчатся вместе

## .art формат

JSON файлы в `%LocalAppData%/ParaTool/Artifacts/*.art`. Это рабочий формат ParaTool, не для обмена между игроками. Результат — готовый .pak для мультиплеера.

**Модель** (`ArtifactDefinition.cs`):
- Identity: StatId, StatType, UsingBase
- Template: TemplateUuid (новый), ParentTemplateUuid (наследуемая модель)
- Properties: Rarity, ValueOverride, ArmorClass, Damage, Weight...
- Mechanics: Boosts, PassivesOnEquip, StatusOnEquip, SpellsOnEquip
- Passives[]: PassiveDefinition (BoostContext/StatsFunctors/Conditions)
- Statuses[]: StatusDefinition (StatusType, StatusGroups, StackType, TickFunctors...)
- Spells[]: SpellDefinition (SpellType, UseCosts, Cooldown...)
- Localization: DisplayName/Description per language (BB-code формат)
- Icons: base64 DDS или MapKey из атласа
- Loot: пул, темы

## BB-code система локализации

Юзер пишет в редакторе BB-code, который конвертируется в BG3 XML-escaped формат для .loca.xml.

### Синтаксис
| BB-code | BG3 эквивалент |
|---------|---------------|
| `[b]...[/b]` | `<b>...</b>` |
| `[br]` | `<br>` |
| `[tip=ArmourClass]КБ[/tip]` | `<LSTag Tooltip="ArmourClass">КБ</LSTag>` |
| `[status=STUNNED]оглушён[/status]` | `<LSTag Type="Status" Tooltip="STUNNED">оглушён</LSTag>` |
| `[spell=Shout_X]name[/spell]` | `<LSTag Type="Spell" Tooltip="Shout_X">name</LSTag>` |
| `[passive=X]name[/passive]` | `<LSTag Type="Passive" Tooltip="X">name</LSTag>` |
| `[resource=KiPoint]Ки[/resource]` | `<LSTag Type="ActionResource" Tooltip="KiPoint">Ки</LSTag>` |
| `[p1]`, `[p2]` | `[1]`, `[2]` (DescriptionParams) |
| `[dp1]`, `[dp2]` | `<b>[1]</b>` (жирный параметр для урона) |

### Тулбар редактора
Кнопки: B, Status▾, Tooltip▾, Spell▾, Resource▾, Param▾, BR
Каждая вставляет BB-code в текстбокс по позиции курсора. Тултипы — выпадающий список из `LsTagDatabase`.

## Иконки

### Структура в BG3 паке
- `Mods/.../GUI/Assets/Tooltips/ItemIcons/{Name}.DDS` — 380×380 DXT5
- `Mods/.../GUI/Assets/ControllerUIIcons/items_png/{Name}.DDS` — 144×144 DXT5
- `Public/.../Assets/Textures/Icons/{Atlas}.dds` — атлас 1440×1440 (10×10 × 144px)
- `Public/.../GUI/Icons_{Atlas}.lsx` — UV координаты

### Пайплайн
PNG → PngReader → RGBA → IconConverter → 3 версии (380×380 DDS, 144×144 DDS, 144×144 для атласа)

### Браузер иконок
- Грид 144×144 превью из атласов AMP и модов
- Кнопка "Загрузить PNG" для своей иконки
- Кнопка "Распаковать ванильные атласы" — извлечение из Game.pak по запросу (тяжёлая операция, кешируется)

## Stats Schema — движок автогенерации GUI

`StatsSchema.cs` парсит embedded resources и знает ВСЕ поля каждого типа:

### Источники данных (embedded resources)
| Файл | Что даёт |
|------|---------|
| `Modifiers.txt` | Все поля для Armor(53), Weapon(62), PassiveData(27), StatusData(122), SpellData(194), InterruptData(30) + типы полей |
| `ValueLists.txt` | Допустимые enum-значения: Rarity, ArmorType, SpellSchool, DamageType, Surfaces, StatusGroups... |
| `LSLibDefinitions.xml` | Дополнительные enumerations |
| `CommonConditions.khn` | Vanilla условия-функции (ManeuverSaveDC, Self, Enemy...) |
| `Khn.HardcodedConditions.lua` | Встроенные в движок условия (IsWeaponAttack, HasDamageDoneForType, SpellId...) |
| `amp_conditions.khn` | AMP-специфичные условия |

### Как GUI использует
```csharp
var typeDef = StatsSchema.Instance.GetType("PassiveData");
foreach (var field in typeDef.Fields)
{
    if (field.IsEnum) → ComboBox с допустимыми значениями
    if (field.IsNumeric) → NumericUpDown
    if (field.Name == "Conditions") → TextBox + автокомплит из SearchConditions()
    if (field.IsTranslatedString) → BB-code редактор
    if (field.IsFlags) → MultiSelect чипы
}
```

## Критические правила BG3 Stats (автовалидация)

Конструктор должен автоматически:
1. **StatusType/SpellType перед using** — в выходном Stats тексте
2. **SELF/SWAP подсказки** — при выборе OnDamaged контекста
3. **DescriptionParams валидация** — если в BB-code есть [pN], проверяем заполненность
4. **DealDamage пробелы** — BB-code конвертер учитывает автоматически
5. **Один handle на все тиры** — если текст одинаковый, только числа разные
6. **LSTag валидация** — запретить [tip=Fire] (типы урона НЕ существуют как LSTag)
7. **Группировка по редкости** — при генерации Stats
8. **IsMiss() → (IsMiss() or IsCriticalMiss())** — предупреждение в Conditions

## Что готово (фундамент)

### LSLib интеграция (13 файлов, MIT лицензия Norbyte)
- LSFReader/LSFWriter — бинарный LSF формат
- LSXReader/LSXWriter — XML формат
- Localization.cs — бинарный .loca + XML loca
- Resource/Node/NodeAttribute — дерево данных
- Compression — LZ4/Zlib/Zstd адаптер (наши NuGet пакеты)

### DDS текстуры (7 файлов)
- BC3 decode/encode, DDS read/write, PNG reader
- IconConverter — PNG→BG3 (380×380 + 144×144 + атлас)
- DdsBitmapConverter — Avalonia WriteableBitmap

### Локализация (3 файла)
- BbCode — BB-code ↔ BG3 XML конвертер
- HandleGenerator — генерация h...g...g... handle'ов
- LsTagDatabase — справочник тегов с русскими переводами

### Артефакты (3 файла)
- ArtifactDefinition — полная модель
- ArtifactStore — хранение в %LocalAppData%
- ArtifactCompiler — генерация Stats/Loca/Icons

### Schema (1 файл + 6 resources)
- StatsSchema — парсит Modifiers/ValueLists/Conditions

### Тесты
- 100 тестов, 0 ошибок

## Что нужно сделать (следующие сессии)

### Этап 1: Интеграция артефактов в патчер
- Загрузка .art из Store при сканировании
- Компиляция через ArtifactCompiler → Stats + Loca + Icons
- Генерация RootTemplate LSF через LSFWriter
- Вписывание в AMP пак при патче (вместе с мод-предметами)
- Добавление в TreasureTable

### Этап 2: GUI Конструктора (основной)
- Вкладка "Конструктор" в нижнем таб-баре
- Левая колонка: дерево предметов (AMP + моды + мои артефакты)
- Центр: редактор свойств — карточки генерируются из StatsSchema
- Правая колонка: превью игрового тултипа (sticky)
- BB-code текстовый редактор с тулбаром
- Создание/удаление пассивок, статусов, заклинаний

### Этап 3: Браузер иконок
- Грид иконок из AMP атласов (DDS decode + нарезка)
- Загрузка PNG → автоконвертация в DDS
- Кнопка "Распаковать ванильные атласы"
- Кеширование распакованных иконок

### Этап 4: Продвинутые фичи
- Тиры (Uncommon→Legendary) — автогенерация дублей через using
- Автокомплит условий из Schema
- Валидация Stats перед патчем
- Предпросмотр сгенерированного Stats текста
