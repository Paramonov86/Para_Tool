using System.Linq;
using System.Collections.Generic;

namespace ParaTool.Core.Schema;

/// <summary>
/// Localized short display labels for all BG3 enum values used in chip editors.
/// Covers all enum arrays in BoostMapping and ConditionSchema.
/// </summary>
public static class EnumLabels
{
    /// <summary>Get a localized short label for any enum value.</summary>
    public static string GetLabel(string value, string lang) =>
        lang == "ru" ? GetRu(value) : GetEn(value);

    /// <summary>Get display labels array matching the enum values array.</summary>
    public static string[] GetDisplayLabels(string[] values, string lang) =>
        values.Select(v => GetLabel(v, lang)).ToArray();

    private static string GetEn(string v) => EnMap.TryGetValue(v, out var pair) ? pair.en : v;
    private static string GetRu(string v) => EnMap.TryGetValue(v, out var pair) ? pair.ru : v;

    private static readonly Dictionary<string, (string en, string ru)> EnMap =
        new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Abilities ──────────────────────────────────────────────
        ["Strength"]              = ("STR",     "СИЛ"),
        ["Dexterity"]             = ("DEX",     "ЛОВ"),
        ["Constitution"]          = ("CON",     "ТЕЛ"),
        ["Intelligence"]          = ("INT",     "ИНТ"),
        ["Wisdom"]                = ("WIS",     "МДР"),
        ["Charisma"]              = ("CHA",     "ХАР"),

        // ── Damage Types ───────────────────────────────────────────
        ["None"]                  = ("None",    "Нет"),
        ["All"]                   = ("All",     "Все"),
        ["Slashing"]              = ("Slash",   "Рубящий"),
        ["Piercing"]              = ("Pierce",  "Колющий"),
        ["Bludgeoning"]           = ("Blunt",   "Дроб."),
        ["Acid"]                  = ("Acid",    "Кислота"),
        ["Thunder"]               = ("Thunder", "Гром"),
        ["Necrotic"]              = ("Necro",   "Некр."),
        ["Fire"]                  = ("Fire",    "Огонь"),
        ["Lightning"]             = ("Lightn.", "Молния"),
        ["Cold"]                  = ("Cold",    "Холод"),
        ["Psychic"]               = ("Psych",  "Псих."),
        ["Poison"]                = ("Poison",  "Яд"),
        ["Radiant"]               = ("Radiant", "Свящ."),
        ["Force"]                 = ("Force",   "Сила"),

        // ── DamageTypes extended ───────────────────────────────────
        ["MainWeaponDamageType"]         = ("Main Wpn",    "Осн. оружие"),
        ["OffhandWeaponDamageType"]      = ("Off Wpn",     "Доп. оружие"),
        ["MainMeleeWeaponDamageType"]    = ("Main Melee",  "Осн. ближн."),
        ["OffhandMeleeWeaponDamageType"] = ("Off Melee",   "Доп. ближн."),
        ["MainRangedWeaponDamageType"]   = ("Main Range",  "Осн. дальн."),
        ["OffhandRangedWeaponDamageType"]= ("Off Range",   "Доп. дальн."),
        ["SourceWeaponDamageType"]       = ("Src Wpn",     "Источн. оруж."),
        ["ThrownWeaponDamageType"]       = ("Thrown",      "Метат."),

        // ── StatsRollType ──────────────────────────────────────────
        ["Attack"]                    = ("Atk",          "Атака"),
        ["MeleeWeaponAttack"]         = ("Melee Atk",    "Ближн. атака"),
        ["RangedWeaponAttack"]        = ("Range Atk",    "Дальн. атака"),
        ["MeleeSpellAttack"]          = ("Melee Spell",  "Ближн. заклин."),
        ["RangedSpellAttack"]         = ("Range Spell",  "Дальн. заклин."),
        ["MeleeUnarmedAttack"]        = ("Melee Unarm",  "Ближн. безор."),
        ["RangedUnarmedAttack"]       = ("Range Unarm",  "Дальн. безор."),
        ["MeleeOffHandWeaponAttack"]  = ("Melee Off",    "Ближн. доп."),
        ["RangedOffHandWeaponAttack"] = ("Range Off",    "Дальн. доп."),
        ["SkillCheck"]                = ("Skill",        "Навык"),
        ["SavingThrow"]               = ("Save",         "Спасбр."),
        ["RawAbility"]                = ("Ability",      "Способн."),
        ["Damage"]                    = ("Damage",       "Урон"),
        ["DeathSavingThrow"]          = ("Death Save",   "Спасбр. смерти"),
        ["MeleeWeaponDamage"]         = ("Melee Dmg",    "Ближн. урон"),
        ["RangedWeaponDamage"]        = ("Range Dmg",    "Дальн. урон"),
        ["MeleeSpellDamage"]          = ("MSpl Dmg",     "Ближн. заклин. ур."),
        ["RangedSpellDamage"]         = ("RSpl Dmg",     "Дальн. заклин. ур."),
        ["MeleeUnarmedDamage"]        = ("Melee Un Dmg", "Ближн. безор. ур."),
        ["RangedUnarmedDamage"]       = ("Range Un Dmg", "Дальн. безор. ур."),

        // ── AttackType ─────────────────────────────────────────────
        ["DirectHit"]                 = ("Direct",       "Прямой"),
        // MeleeWeaponAttack, RangedWeaponAttack, etc. already covered above
        ["MeleeOffHandWeaponAttack"]  = ("Melee Off",    "Ближн. доп."),
        ["RangedOffHandWeaponAttack"] = ("Range Off",    "Дальн. доп."),

        // ── AdvantageContext ───────────────────────────────────────
        ["AttackRoll"]           = ("Atk Roll",     "Бросок атаки"),
        ["AttackTarget"]         = ("Atk Target",   "Цель атаки"),
        ["AllSavingThrows"]      = ("All Saves",    "Все спасбр."),
        ["Ability"]              = ("Ability",      "Способность"),
        ["AllAbilities"]         = ("All Abil.",    "Все способн."),
        ["Skill"]                = ("Skill",        "Навык"),
        ["AllSkills"]            = ("All Skills",   "Все навыки"),
        ["SourceDialogue"]       = ("Dialogue",     "Диалог"),
        ["DeathSavingThrow"]     = ("Death Save",   "Спасбр. смерти"),
        ["Concentration"]        = ("Concent.",     "Концентр."),

        // ── SkillType ──────────────────────────────────────────────
        ["Deception"]            = ("Decep.",       "Обман"),
        ["Intimidation"]         = ("Intim.",       "Запугив."),
        ["Performance"]          = ("Perform.",     "Выступл."),
        ["Persuasion"]           = ("Persuad.",     "Убежд."),
        ["Acrobatics"]           = ("Acrobat.",     "Акробат."),
        ["SleightOfHand"]        = ("Sleight",      "Лов. рук"),
        ["Stealth"]              = ("Stealth",      "Скрытн."),
        ["Arcana"]               = ("Arcana",       "Магия"),
        ["History"]              = ("History",      "История"),
        ["Investigation"]        = ("Invest.",      "Расследов."),
        ["Nature"]               = ("Nature",       "Природа"),
        ["Religion"]             = ("Religion",     "Религия"),
        ["Athletics"]            = ("Athletics",    "Атлетика"),
        ["AnimalHandling"]       = ("Animal",       "Обращ. с жив."),
        ["Insight"]              = ("Insight",      "Проницат."),
        ["Medicine"]             = ("Medicine",     "Медицина"),
        ["Perception"]           = ("Percep.",      "Восприятие"),
        ["Survival"]             = ("Survival",     "Выживание"),

        // ── ResistanceBoostFlags ───────────────────────────────────
        ["Resistant"]              = ("Resist.",      "Устойч."),
        ["Immune"]                 = ("Immune",       "Невосприимч."),
        ["Vulnerable"]             = ("Vuln.",        "Уязвим."),
        ["BelowDamageThreshold"]   = ("< Threshold",  "< Порога"),
        ["ResistantToMagical"]     = ("Resist. Mag",  "Устойч. к маг."),
        ["ImmuneToMagical"]        = ("Imm. Mag",     "Невосп. к маг."),
        ["VulnerableToMagical"]    = ("Vuln. Mag",    "Уязв. к маг."),
        ["ResistantToNonMagical"]  = ("Resist. NMag", "Устойч. не-маг."),
        ["ImmuneToNonMagical"]     = ("Imm. NMag",    "Невосп. не-маг."),
        ["VulnerableToNonMagical"] = ("Vuln. NMag",   "Уязв. не-маг."),

        // ── ProficiencyBonusBoostType ──────────────────────────────
        // AttackRoll, AttackTarget, SavingThrow, AllSavingThrows, Ability,
        // AllAbilities, Skill, AllSkills, SourceDialogue, WeaponActionDC — most covered
        ["WeaponActionDC"]       = ("Wpn DC",       "КС оружия"),

        // ── CriticalHitType ────────────────────────────────────────
        // AttackTarget, AttackRoll already covered

        // ── CriticalHitResult ──────────────────────────────────────
        ["Success"]              = ("Success",      "Успех"),
        ["Failure"]              = ("Failure",      "Провал"),

        // ── CriticalHitWhen ────────────────────────────────────────
        ["Never"]                = ("Never",        "Никогда"),
        ["Always"]               = ("Always",       "Всегда"),
        ["ForcedAlways"]         = ("Forced",       "Принудит."),

        // ── DamageReductionType ────────────────────────────────────
        ["Half"]                 = ("Half",         "Половина"),
        ["Flat"]                 = ("Flat",         "Фикс."),
        ["Threshold"]            = ("Threshold",    "Порог"),

        // ── WeaponFlags ────────────────────────────────────────────
        ["Light"]                = ("Light",        "Лёгкое"),
        ["Ammunition"]           = ("Ammo",         "Боепр."),
        ["Finesse"]              = ("Finesse",      "Фехт."),
        ["Heavy"]                = ("Heavy",        "Тяжёлое"),
        ["Loading"]              = ("Loading",      "Зарядка"),
        ["Range"]                = ("Range",        "Дальнобойн."),
        ["Reach"]                = ("Reach",        "Досягаем."),
        ["Lance"]                = ("Lance",        "Копьё"),
        ["Net"]                  = ("Net",          "Сеть"),
        ["Thrown"]               = ("Thrown",       "Метат."),
        ["Twohanded"]            = ("2H",           "Двуручное"),
        ["Versatile"]            = ("Versatile",    "Универс."),
        ["Melee"]                = ("Melee",        "Ближнее"),
        ["Dippable"]             = ("Dippable",     "Маканье"),
        ["Torch"]                = ("Torch",        "Факел"),
        ["NoDualWield"]          = ("No DW",        "Нет двуручья"),
        ["Magical"]              = ("Magical",      "Магич."),
        ["NeedDualWieldingBoost"]= ("Need DW",      "Треб. двуручье"),
        ["NotSheathable"]        = ("No Sheath",    "Нельзя убрать"),
        ["Unstowable"]           = ("Unstow.",      "Нельзя спрятать"),
        ["AddToHotbar"]          = ("Hotbar",       "В панель"),

        // ── ArmorTypes ─────────────────────────────────────────────
        ["Cloth"]                = ("Cloth",        "Ткань"),
        ["Padded"]               = ("Padded",       "Стёганая"),
        ["Leather"]              = ("Leather",      "Кожаная"),
        ["StuddedLeather"]       = ("Studded",      "Клёпаная"),
        ["Hide"]                 = ("Hide",         "Шкура"),
        ["ChainShirt"]           = ("Chain Shirt",  "Кольч. рубашка"),
        ["ScaleMail"]            = ("Scale",        "Чешуйч."),
        ["BreastPlate"]          = ("Breastpl.",    "Нагрудник"),
        ["HalfPlate"]            = ("Half Plate",   "Полулат."),
        ["RingMail"]             = ("Ring Mail",    "Кольч. панц."),
        ["ChainMail"]            = ("Chain Mail",   "Кольчуга"),
        ["Splint"]               = ("Splint",       "Пластин."),
        ["Plate"]                = ("Plate",        "Латы"),

        // ── SpellSchool ────────────────────────────────────────────
        ["Abjuration"]           = ("Abjur.",       "Ограждение"),
        ["Conjuration"]          = ("Conjur.",      "Вызов"),
        ["Divination"]           = ("Divin.",       "Прорицание"),
        ["Enchantment"]          = ("Enchant.",     "Очарование"),
        ["Evocation"]            = ("Evoc.",        "Воплощение"),
        ["Illusion"]             = ("Illus.",       "Иллюзия"),
        ["Necromancy"]           = ("Necro.",       "Некромантия"),
        ["Transmutation"]        = ("Transm.",      "Преобразование"),

        // ── SpellCooldownType ──────────────────────────────────────
        ["Default"]              = ("Default",      "По умолч."),
        ["OncePerTurn"]          = ("1/Turn",       "1/ход"),
        ["OncePerCombat"]        = ("1/Combat",     "1/бой"),
        ["UntilRest"]            = ("Until Rest",   "До отдыха"),
        ["OncePerTurnNoRealtime"]= ("1/Turn RT",    "1/ход (РВ)"),
        ["UntilShortRest"]       = ("Short Rest",   "До кор. отдыха"),
        ["UntilPerRestPerItem"]  = ("Rest/Item",    "До отд./предм."),
        ["OncePerShortRestPerItem"]=("1/SR/Item",   "1/КО/предм."),

        // ── UnlockSpellType ────────────────────────────────────────
        ["Singular"]             = ("Singular",     "Одиночн."),
        ["AddChildren"]          = ("Add Children", "Доб. дочерн."),
        ["MostPowerful"]         = ("Most Pow.",    "Мощнейшее"),

        // ── HealingDirection ───────────────────────────────────────
        ["Incoming"]             = ("Incoming",     "Входящ."),
        ["Outgoing"]             = ("Outgoing",     "Исходящ."),

        // ── MovementSpeedType ──────────────────────────────────────
        ["Stroll"]               = ("Stroll",       "Прогулка"),
        ["Walk"]                 = ("Walk",         "Ходьба"),
        ["Run"]                  = ("Run",          "Бег"),
        ["Sprint"]               = ("Sprint",       "Спринт"),

        // ── SurfaceTypes ───────────────────────────────────────────
        ["Water"]                = ("Water",        "Вода"),
        ["WaterElectrified"]     = ("Water Elec",   "Вода (эл.)"),
        ["WaterFrozen"]          = ("Ice",          "Лёд"),
        ["Blood"]                = ("Blood",        "Кровь"),
        ["BloodElectrified"]     = ("Blood Elec",   "Кровь (эл.)"),
        ["BloodFrozen"]          = ("Blood Ice",    "Кровь (лёд)"),
        ["Poison"]               = ("Poison",       "Яд"),
        ["Oil"]                  = ("Oil",          "Масло"),
        ["Lava"]                 = ("Lava",         "Лава"),
        ["Grease"]               = ("Grease",       "Жир"),
        ["Web"]                  = ("Web",          "Паутина"),
        ["Deepwater"]            = ("Deep Water",   "Глубок. вода"),
        ["Vines"]                = ("Vines",        "Лозы"),
        // Fire, Acid already covered
        ["Mud"]                  = ("Mud",          "Грязь"),
        ["Alcohol"]              = ("Alcohol",      "Алкоголь"),

        // ── InSurfaceValues (strip "Surface" prefix for EN/RU) ─────
        ["SurfaceNone"]                     = ("None",        "Нет"),
        ["SurfaceWater"]                    = ("Water",       "Вода"),
        ["SurfaceWaterElectrified"]         = ("Water Elec",  "Вода (эл.)"),
        ["SurfaceWaterFrozen"]              = ("Ice",         "Лёд"),
        ["SurfaceBlood"]                    = ("Blood",       "Кровь"),
        ["SurfaceBloodElectrified"]         = ("Blood Elec",  "Кровь (эл.)"),
        ["SurfaceBloodFrozen"]              = ("Blood Ice",   "Кровь (лёд)"),
        ["SurfacePoison"]                   = ("Poison",      "Яд"),
        ["SurfaceOil"]                      = ("Oil",         "Масло"),
        ["SurfaceLava"]                     = ("Lava",        "Лава"),
        ["SurfaceGrease"]                   = ("Grease",      "Жир"),
        ["SurfaceWeb"]                      = ("Web",         "Паутина"),
        ["SurfaceDeepwater"]                = ("Deep Water",  "Глубок. вода"),
        ["SurfaceFire"]                     = ("Fire",        "Огонь"),
        ["SurfaceAcid"]                     = ("Acid",        "Кислота"),
        ["SurfaceMud"]                      = ("Mud",         "Грязь"),
        ["SurfaceAlcohol"]                  = ("Alcohol",     "Алкоголь"),
        ["SurfaceHellfire"]                 = ("Hellfire",    "Адское пламя"),
        ["SurfaceAsh"]                      = ("Ash",         "Пепел"),
        ["SurfaceSpikeGrowth"]              = ("Spikes",      "Шипы"),
        ["SurfaceHolyFire"]                 = ("Holy Fire",   "Свящ. огонь"),
        ["SurfaceBlackTentacles"]           = ("Tentacles",   "Щупальца"),
        ["SurfaceOvergrowth"]               = ("Overgrowth",  "Поросль"),
        ["SurfaceWaterCloud"]               = ("Water Cld",   "Обл. воды"),
        ["SurfaceWaterCloudElectrified"]    = ("W.Cld Elec",  "Обл. воды (эл.)"),
        ["SurfacePoisonCloud"]              = ("Psn Cloud",   "Обл. яда"),
        ["SurfaceCloudkillCloud"]           = ("Cloudkill",   "Смертельный туман"),
        ["SurfaceDarknessCloud"]            = ("Darkness",    "Тьма"),
        ["SurfaceFogCloud"]                 = ("Fog",         "Туман"),
        ["SurfaceIceCloud"]                 = ("Ice Cloud",   "Обл. льда"),
        ["SurfaceSentinel"]                 = ("Sentinel",    "Страж"),
        ["SurfaceBladeBarrier"]             = ("Blade Barr.", "Стена клинков"),
        ["SurfaceCausticBrine"]             = ("Caust. Brine","Едкий рассол"),
        ["SurfaceWaterDeepRunning"]         = ("Deep Run",    "Глуб. поток"),
        ["SurfaceWaterRunning"]             = ("Running",     "Поток"),
        ["SurfaceSurfaceDeepWater"]         = ("Surf. Deep",  "Глуб. вода (пов.)"),
        ["SurfaceSurfaceDeepWaterRunning"]  = ("Deep Run2",   "Глуб. поток (пов.)"),
        ["SurfaceSurfaceWaterElectrified"]  = ("W.Elec2",     "Вода (эл.) (пов.)"),

        // ── SurfaceChange ──────────────────────────────────────────
        ["Ignite"]               = ("Ignite",       "Поджечь"),
        ["Douse"]                = ("Douse",        "Потушить"),
        ["Electrify"]            = ("Electrify",    "Электриф."),
        ["Deelectrify"]          = ("Deelect.",     "Снять эл."),
        ["Freeze"]               = ("Freeze",       "Заморозить"),
        ["Melt"]                 = ("Melt",         "Растопить"),
        ["Vaporize"]             = ("Vaporize",     "Испарить"),
        ["Condense"]             = ("Condense",     "Сконденс."),
        ["DestroyWater"]         = ("Destr. Water", "Уничт. воду"),
        ["Clear"]                = ("Clear",        "Очистить"),

        // ── ZoneShape ─────────────────────────────────────────────
        ["Cone"]                 = ("Cone",         "Конус"),
        ["Square"]               = ("Square",       "Квадрат"),

        // ── ForceFunctorOrigin ────────────────────────────────────
        ["OriginToEntity"]       = ("Orig→Ent",     "Источн.→Сущ."),
        ["OriginToTarget"]       = ("Orig→Tgt",     "Источн.→Цель"),
        ["TargetToEntity"]       = ("Tgt→Ent",      "Цель→Сущ."),

        // ── ForceFunctorAggression ────────────────────────────────
        ["Aggressive"]           = ("Aggress.",     "Агрессив."),
        ["Friendly"]             = ("Friendly",     "Дружеск."),
        ["Neutral"]              = ("Neutral",      "Нейтральн."),

        // ── DeathTypes ────────────────────────────────────────────
        // Acid, None already covered
        ["Chasm"]                = ("Chasm",        "Бездна"),
        ["DoT"]                  = ("DoT",          "Перманент."),
        ["Electrocution"]        = ("Electro.",     "Электрок."),
        ["Explode"]              = ("Explode",      "Взрыв"),
        ["Falling"]              = ("Falling",      "Падение"),
        ["Incinerate"]           = ("Incinerate",   "Сожжение"),
        ["KnockedDown"]          = ("Knocked",      "Сбит с ног"),
        ["Lifetime"]             = ("Lifetime",     "Время жизни"),
        ["Narcolepsy"]           = ("Narcol.",      "Нарколепсия"),
        ["PetrifiedShattered"]   = ("Petrified",    "Окам. + осколки"),
        ["Sentinel"]             = ("Sentinel",     "Страж"),

        // ── ResurrectTypes ────────────────────────────────────────
        ["Living"]               = ("Living",       "Живое"),
        ["Guaranteed"]           = ("Guaranteed",   "Гарантир."),
        ["Construct"]            = ("Construct",    "Конструкт"),
        ["Undead"]               = ("Undead",       "Нежить"),

        // ── SurfaceLayers ─────────────────────────────────────────
        ["Ground"]               = ("Ground",       "Земля"),
        ["Cloud"]                = ("Cloud",        "Облако"),

        // ── ExecuteWeaponFunctorsType ─────────────────────────────
        ["MainHand"]             = ("Main Hand",    "Осн. рука"),
        ["OffHand"]              = ("Off Hand",     "Доп. рука"),
        ["BothHands"]            = ("Both Hands",   "Обе руки"),

        // ── StatItemSlot ──────────────────────────────────────────
        ["Helmet"]               = ("Helmet",       "Шлем"),
        ["Breast"]               = ("Chest",        "Нагрудник"),
        ["Cloak"]                = ("Cloak",        "Плащ"),
        ["MeleeMainHand"]        = ("M.Main",       "Ближн. осн."),
        ["MeleeOffHand"]         = ("M.Off",        "Ближн. доп."),
        ["RangedMainHand"]       = ("R.Main",       "Дальн. осн."),
        ["RangedOffHand"]        = ("R.Off",        "Дальн. доп."),
        ["Ring"]                 = ("Ring",         "Кольцо"),
        ["Underwear"]            = ("Underwear",    "Нижн. бельё"),
        ["Boots"]                = ("Boots",        "Сапоги"),
        ["Gloves"]               = ("Gloves",       "Перчатки"),
        ["Amulet"]               = ("Amulet",       "Амулет"),
        ["Ring2"]                = ("Ring 2",       "Кольцо 2"),
        ["Wings"]                = ("Wings",        "Крылья"),
        ["Horns"]                = ("Horns",        "Рога"),
        ["Overhead"]             = ("Overhead",     "Над головой"),
        ["MusicalInstrument"]    = ("Instrument",   "Инструмент"),
        ["VanityBody"]           = ("Vanity Body",  "Скин тела"),
        ["VanityBoots"]          = ("Vanity Boots", "Скин обуви"),
        ["OffHand"]              = ("Off Hand",     "Доп. рука"),

        // ── SetStatusDurationType ─────────────────────────────────
        ["SetMinimum"]           = ("Set Min",      "Уст. мин."),
        ["ForceSet"]             = ("Force Set",    "Принудит."),
        ["Add"]                  = ("Add",          "Добавить"),
        ["Multiply"]             = ("Multiply",     "Умножить"),

        // ── RollAdjustmentType ────────────────────────────────────
        // All already covered
        ["Distribute"]           = ("Distribute",   "Распредел."),

        // ── AttributeFlags ────────────────────────────────────────
        ["SlippingImmunity"]     = ("No Slip",      "Невосп. скольж."),
        // Torch already covered
        ["Arrow"]                = ("Arrow",        "Стрела"),
        ["Unbreakable"]          = ("Unbreak.",     "Несломл."),
        ["Grounded"]             = ("Grounded",     "Заземлён"),
        ["Floating"]             = ("Floating",     "Парение"),
        ["InventoryBound"]       = ("Inv. Bound",   "Привязан к инв."),
        ["IgnoreClouds"]         = ("No Clouds",    "Игнор. облака"),
        ["BackstabImmunity"]     = ("No Backstab",  "Невосп. удар в спину"),
        ["ThrownImmunity"]       = ("No Thrown",    "Невосп. метанию"),
        ["InvisibilityImmunity"] = ("No Invis",     "Невосп. невид."),

        // ── ProficiencyGroupFlags ─────────────────────────────────
        ["LightArmor"]           = ("Lt Armor",     "Лёгк. броня"),
        ["MediumArmor"]          = ("Med Armor",    "Сред. броня"),
        ["HeavyArmor"]           = ("Hvy Armor",    "Тяж. броня"),
        ["Shields"]              = ("Shields",      "Щиты"),
        ["SimpleMeleeWeapon"]    = ("Sim. Melee",   "Прост. ближн."),
        ["SimpleRangedWeapon"]   = ("Sim. Range",   "Прост. дальн."),
        ["MartialMeleeWeapon"]   = ("Mar. Melee",   "Воен. ближн."),
        ["MartialRangedWeapon"]  = ("Mar. Range",   "Воен. дальн."),
        ["HandCrossbows"]        = ("Hand XBow",    "Ручн. арбал."),
        ["Battleaxes"]           = ("Battleaxe",    "Боевой топор"),
        ["Flails"]               = ("Flail",        "Цеп"),
        ["Glaives"]              = ("Glaive",       "Глевия"),
        ["Greataxes"]            = ("Greataxe",     "Секира"),
        ["Greatswords"]          = ("Greatsword",   "Двуруч. меч"),
        ["Halberds"]             = ("Halberd",      "Алебарда"),
        ["Longswords"]           = ("Longsword",    "Длин. меч"),
        ["Mauls"]                = ("Maul",         "Молот"),
        ["Morningstars"]         = ("Morningstar",  "Моргенштерн"),
        ["Pikes"]                = ("Pike",         "Пика"),
        ["Rapiers"]              = ("Rapier",       "Рапира"),
        ["Scimitars"]            = ("Scimitar",     "Ятаган"),
        ["Shortswords"]          = ("Shortsword",   "Корот. меч"),
        ["Tridents"]             = ("Trident",      "Трезубец"),
        ["WarPicks"]             = ("War Pick",     "Боевая кирка"),
        ["Warhammers"]           = ("Warhammer",    "Боевой молот"),
        ["Clubs"]                = ("Club",         "Дубина"),
        ["Daggers"]              = ("Dagger",       "Кинжал"),
        ["Greatclubs"]           = ("Greatclub",    "Большая дубина"),
        ["Handaxes"]             = ("Handaxe",      "Ручной топор"),
        ["Javelins"]             = ("Javelin",      "Дротик"),
        ["LightHammers"]         = ("Lt Hammer",    "Лёгкий молот"),
        ["Maces"]                = ("Mace",         "Булава"),
        ["Quarterstaffs"]        = ("Quarterstaff", "Посох"),
        ["Sickles"]              = ("Sickle",       "Серп"),
        ["Spears"]               = ("Spear",        "Копьё"),
        ["LightCrossbows"]       = ("Lt XBow",      "Лёгк. арбал."),
        ["Darts"]                = ("Dart",         "Дротик (малый)"),
        ["Shortbows"]            = ("Shortbow",     "Короткий лук"),
        ["Slings"]               = ("Sling",        "Праща"),
        ["Longbows"]             = ("Longbow",      "Длинный лук"),
        ["HeavyCrossbows"]       = ("Hvy XBow",     "Тяж. арбал."),
        // MusicalInstrument already covered

        // ── SummonDurations ───────────────────────────────────────
        ["UntilLongRest"]        = ("Long Rest",    "До длин. отдыха"),
        ["Permanent"]            = ("Permanent",    "Постоянно"),

        // ── MagicalFlags ──────────────────────────────────────────
        ["Nonmagical"]           = ("Non-Mag.",     "Немагич."),

        // ── NonlethalFlags ────────────────────────────────────────
        ["Lethal"]               = ("Lethal",       "Смертельный"),
        ["Nonlethal"]            = ("Non-Lethal",   "Несмертельный"),

        // ── SizeCategories ────────────────────────────────────────
        ["Tiny"]                 = ("Tiny",         "Крохотный"),
        ["Small"]                = ("Small",        "Маленький"),
        ["Medium"]               = ("Medium",       "Средний"),
        ["Large"]                = ("Large",        "Большой"),
        ["Huge"]                 = ("Huge",         "Огромный"),
        ["Gargantuan"]           = ("Gargant.",     "Исполинский"),

        // ── EngineStatusTypes ─────────────────────────────────────
        ["DYING"]                = ("Dying",        "Умирает"),
        ["HEAL"]                 = ("Heal",         "Исцеление"),
        ["KNOCKED_DOWN"]         = ("Knocked",      "Сбит"),
        ["TELEPORT_FALLING"]     = ("Tele Fall",    "Телепорт/Падение"),
        ["BOOST"]                = ("Boost",        "Буст"),
        ["REACTION"]             = ("Reaction",     "Реакция"),
        ["STORY_FROZEN"]         = ("Frozen",       "Заморожен"),
        ["SNEAKING"]             = ("Sneaking",     "Подкрад."),
        ["UNLOCK"]               = ("Unlock",       "Разблок."),
        ["FEAR"]                 = ("Fear",         "Страх"),
        ["SMELLY"]               = ("Smelly",       "Вонючий"),
        ["INVISIBLE"]            = ("Invisible",    "Невидим."),
        ["ROTATE"]               = ("Rotate",       "Поворот"),
        ["MATERIAL"]             = ("Material",     "Материал"),
        ["CLIMBING"]             = ("Climbing",     "Лазание"),
        ["INCAPACITATED"]        = ("Incap.",       "Недееспос."),
        ["INSURFACE"]            = ("In Surface",   "В поверхн."),
        ["POLYMORPHED"]          = ("Poly.",        "Полиморф"),
        ["EFFECT"]               = ("Effect",       "Эффект"),
        ["DEACTIVATED"]          = ("Deact.",       "Деактивир."),
        ["DOWNED"]               = ("Downed",       "Повержен"),

        // ── StatusRemoveCause ─────────────────────────────────────
        ["Condition"]            = ("Condition",    "Условие"),
        ["TimeOut"]              = ("Time Out",     "Истечение"),
        ["Death"]                = ("Death",        "Смерть"),

        // ── ObscuredState ─────────────────────────────────────────
        // Clear already covered
        ["BabyBent"]             = ("Baby Bent",    "Полуприсед"),
        ["BentQuarters"]         = ("Bent ½",       "Полуприсед ½"),
        ["ThreeQuarters"]        = ("¾ Cover",      "¾ укрытия"),
        ["FullCover"]            = ("Full Cover",   "Полное укрытие"),

        // ── ActionResources ───────────────────────────────────────
        ["ActionPoint"]          = ("AP",           "ОД"),
        ["BonusActionPoint"]     = ("Bonus AP",     "Бонус ОД"),
        ["Movement"]             = ("Move",         "Движение"),
        ["SpellSlot"]            = ("Spell Slot",   "Ячейка"),
        ["KiPoint"]              = ("Ki",           "Ки"),
        ["Rage"]                 = ("Rage",         "Ярость"),
        ["SorceryPoint"]         = ("Sorc. Pt",     "Мага оч."),
        ["BardicInspiration"]    = ("Bardic Insp",  "Вдохн. барда"),
        ["SuperiorityDie"]       = ("Sup. Die",     "Куб превосх."),
        ["ChannelDivinity"]      = ("Channel Div",  "Канал свящ."),
        ["LayOnHandsCharge"]     = ("Lay Hands",    "Нал. рук"),
        ["WildShape"]            = ("Wild Shape",   "Дикий облик"),
        ["NaturalRecovery"]      = ("Nat. Rec.",    "Природн. восст."),

        // ── SpellFlags (from ConditionSchema) ─────────────────────
        ["Spell"]                = ("Spell",        "Заклинание"),
        ["Cantrip"]              = ("Cantrip",      "Заговор"),
        // Melee already covered
        ["Ranged"]               = ("Ranged",       "Дальнобойн."),
        ["HasHighGroundRangeExtension"] = ("Hi Ground", "Высота +дист."),
        ["IsConcentration"]      = ("Concent.",     "Концентр."),
        ["HasVerbalComponent"]   = ("Verbal",       "Вербальн."),
        ["HasSomaticComponent"]  = ("Somatic",      "Соматич."),

        // ── DamageFlags (from ConditionSchema) ────────────────────
        ["Hit"]                  = ("Hit",          "Попадание"),
        ["Miss"]                 = ("Miss",         "Промах"),
        ["Critical"]             = ("Crit",         "Крит"),
        // Magical, NonLethal, Melee, Ranged, Poison already covered
        ["WeaponBasedDamage"]    = ("Wpn Dmg",      "Урон оружия"),
        ["Surface"]              = ("Surface",      "Поверхность"),
        ["Projectile"]           = ("Projectile",   "Снаряд"),
        ["Trap"]                 = ("Trap",         "Ловушка"),
        ["Thorns"]               = ("Thorns",       "Шипы"),
        ["NonLethal"]            = ("Non-Lethal",   "Несмертельн."),

        // ── SpellCategories (from ConditionSchema) ────────────────
        ["SpellCategory.Dash"]              = ("Dash",         "Рывок"),
        ["SpellCategory.Jump"]              = ("Jump",         "Прыжок"),
        ["SpellCategory.DetectThoughts"]    = ("Detect Tgt",   "Читать мысли"),
        ["SpellCategory.None"]              = ("None",         "Нет"),
        ["SpellCategory.TargetSingle"]      = ("Single",       "1 цель"),
        ["SpellCategory.TargetMultiselect"] = ("Multi",        "Мульти-цель"),
        ["SpellCategory.TargetAoE"]         = ("AoE",          "АОЕ"),
        ["SpellCategory.IntentDamage"]      = ("Dmg",          "Урон"),
        ["SpellCategory.IntentHealing"]     = ("Heal",         "Исцеление"),
        ["SpellCategory.IntentBuff"]        = ("Buff",         "Усил."),
        ["SpellCategory.IntentDebuff"]      = ("Debuff",       "Ослабл."),
        ["SpellCategory.IntentUtility"]     = ("Utility",      "Утилита"),

        // ── SpellTypes (from ConditionSchema) ─────────────────────
        ["SpellType.Damage"]        = ("Dmg",           "Урон"),
        ["SpellType.Healing"]       = ("Heal",          "Исцеление"),
        ["SpellType.Rush"]          = ("Rush",          "Бросок"),
        ["SpellType.Shout"]         = ("Shout",         "Клич"),
        ["SpellType.Zone"]          = ("Zone",          "Зона"),
        ["SpellType.Throw"]         = ("Throw",         "Метание"),
        ["SpellType.Wall"]          = ("Wall",          "Стена"),
        ["SpellType.Teleportation"] = ("Tele.",         "Телепорт"),
        ["SpellType.MultiStrike"]   = ("Multi-Strike",  "Мультиудар"),

        // ── InstrumentTypes ───────────────────────────────────────
        ["Bagpipes"]             = ("Bagpipes",     "Волынка"),
        ["Drum"]                 = ("Drum",         "Барабан"),
        ["Dulcimer"]             = ("Dulcimer",     "Цимбалы"),
        ["Flute"]                = ("Flute",        "Флейта"),
        ["Lute"]                 = ("Lute",         "Лютня"),
        ["Lyre"]                 = ("Lyre",         "Лира"),
        ["Horn"]                 = ("Horn",         "Рог"),
        ["Shawm"]                = ("Shawm",        "Шалмей"),
        ["Violin"]               = ("Violin",       "Скрипка"),

        // ── HealingTypes ──────────────────────────────────────────
        ["Healing"]              = ("Healing",      "Исцеление"),
        ["HealSelf"]             = ("Heal Self",    "Исцелить себя"),
        ["HealSharing"]          = ("Heal Share",   "Исцел. (доля)"),

        // ── StatusRemoveCauses ────────────────────────────────────
        // None, Death already covered
        ["LongRest"]             = ("Long Rest",    "Длин. отдых"),
        ["ShortRest"]            = ("Short Rest",   "Кор. отдых"),
        ["Expired"]              = ("Expired",      "Истёкший"),

        // ── StatusGroups ──────────────────────────────────────────
        ["SG_Condition"]         = ("Condition",    "Состояние"),
        ["SG_Blinded"]           = ("Blinded",      "Ослеплён"),
        ["SG_Charmed"]           = ("Charmed",      "Очарован"),
        ["SG_Cursed"]            = ("Cursed",       "Проклят"),
        ["SG_Disease"]           = ("Disease",      "Болезнь"),
        ["SG_Frightened"]        = ("Frightened",   "Напуган"),
        ["SG_Invisible"]         = ("Invisible",    "Невидим"),
        ["SG_Poisoned"]          = ("Poisoned",     "Отравлен"),
        ["SG_Restrained"]        = ("Restrained",   "Стеснён"),
        ["SG_Stunned"]           = ("Stunned",      "Оглушён"),
        ["SG_Polymorph"]         = ("Polymorph",    "Полиморф"),
        ["SG_Paralyzed"]         = ("Paralyzed",    "Парализован"),
        ["SG_Petrified"]         = ("Petrified",    "Окаменел"),
        ["SG_Rage"]              = ("Rage",         "Ярость"),
        ["SG_Taunted"]           = ("Taunted",      "Спровоцирован"),
        ["SG_Dominated"]         = ("Dominated",    "Подчинён"),
        ["SG_Confused"]          = ("Confused",     "Смятён"),
        ["SG_Mad"]               = ("Mad",          "Безумный"),
        ["SG_HexbladeCurse"]     = ("Hex Curse",    "Проклятие ведьмака"),
        ["SG_Sleeping"]          = ("Sleeping",     "Спит"),
        ["SG_Prone"]             = ("Prone",        "Ничком"),
        ["SG_Unconscious"]       = ("Unconscious",  "Без сознания"),
        ["SG_Silenced"]          = ("Silenced",     "Молчание"),
        ["SG_Incapacitated"]     = ("Incap.",       "Недееспос."),
        ["SG_Drunk"]             = ("Drunk",        "Пьян"),
        ["SG_Exhausted"]         = ("Exhausted",    "Истощён"),
        ["SG_Dazed"]             = ("Dazed",        "Оглоушен"),

        // ── Passive Properties ───────────────────────────────────
        ["Highlighted"]                 = ("Highlight",    "Подсветка"),
        ["IsHidden"]                    = ("Hidden",       "Скрыта"),
        ["IsToggled"]                   = ("Toggle",       "Вкл/Выкл"),
        ["ToggledDefaultOn"]            = ("Default On",   "Вкл по умолч."),
        ["ToggledDefaultAddToHotbar"]   = ("To Hotbar",    "В панель"),
        ["ToggleForParty"]              = ("For Party",    "Для группы"),
        ["OncePerTurn"]                 = ("1/Turn",       "1/Ход"),
        ["OncePerAttack"]               = ("1/Attack",     "1/Атака"),
        ["OncePerShortRest"]            = ("1/Short",      "1/Кор.отдых"),
        ["OncePerLongRest"]             = ("1/Long",       "1/Длин.отдых"),
        ["OncePerCombat"]               = ("1/Combat",     "1/Бой"),
        ["DisplayBoostInTooltip"]       = ("Show Boost",   "Показ. буст"),
        ["Temporary"]                   = ("Temp",         "Врем."),

        // ── ConditionRollType ────────────────────────────────────
        ["ConditionSavingThrow"]         = ("Saving Throw",     "Спасбросок"),
        ["ConditionAbilityCheck"]        = ("Ability Check",    "Проверка способн."),
        ["ConditionAttackRoll"]          = ("Attack Roll",      "Бросок атаки"),
        ["ConditionDeathSavingThrow"]    = ("Death Save",       "Спасбросок смерти"),

        // ── ActionType ───────────────────────────────────────────
        ["MainAction"]                   = ("Action",           "Действие"),
        ["BonusAction"]                  = ("Bonus",            "Бонусное"),
        ["ReAction"]                     = ("Reaction",         "Реакция"),
        ["FreeAction"]                   = ("Free",             "Свободное"),

        // ── DiceType ─────────────────────────────────────────────
        ["D4"]                           = ("d4",               "d4"),
        ["D6"]                           = ("d6",               "d6"),
        ["D8"]                           = ("d8",               "d8"),
        ["D10"]                          = ("d10",              "d10"),
        ["D12"]                          = ("d12",              "d12"),
        ["D20"]                          = ("d20",              "d20"),
    };
}
