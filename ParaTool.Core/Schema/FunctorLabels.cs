namespace ParaTool.Core.Schema;

/// <summary>
/// Human-readable EN+RU labels for BG3 Stats Functors, Boosts, and all their enum arguments.
/// Comprehensive mapping from LSLibDefinitions.xml + AMP stat patterns.
/// Used by BoostBlocksEditor / ConditionBlocksEditor to show friendly names.
/// </summary>
public static class FunctorLabels
{
    // ═══════════════════════════════════════════════════════════
    // FUNCTOR LABELS — (English, Russian)
    // All 60 functors from LSLibDefinitions.xml
    // ═══════════════════════════════════════════════════════════

    public static readonly Dictionary<string, (string En, string Ru)> Functors = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Damage & Healing ──────────────────────────────────
        ["DealDamage"] = ("Deal Damage", "Нанести урон"),
        ["RegainHitPoints"] = ("Heal", "Исцеление"),
        ["GainTemporaryHitPoints"] = ("Temp HP", "Временные ОЗ"),
        ["RegainTemporaryHitPoints"] = ("Regain Temp HP", "Восстановить врем. ОЗ"),

        // ── Status Effects ────────────────────────────────────
        ["ApplyStatus"] = ("Apply Status", "Наложить статус"),
        ["ApplyEquipmentStatus"] = ("Equip Status", "Статус экипировки"),
        ["RemoveStatus"] = ("Remove Status", "Снять статус"),
        ["RemoveUniqueStatus"] = ("Remove Unique Status", "Снять уник. статус"),
        ["RemoveStatusByLevel"] = ("Remove Status by Lvl", "Снять статус по ур."),
        ["RemoveAuraByChildStatus"] = ("Remove Aura by Child", "Снять ауру по потомку"),
        ["SetStatusDuration"] = ("Set Status Duration", "Длительность статуса"),

        // ── Resources ─────────────────────────────────────────
        ["RestoreResource"] = ("Restore Resource", "Восстановить ресурс"),
        ["UseActionResource"] = ("Use Resource", "Потратить ресурс"),

        // ── Surface & Zone ────────────────────────────────────
        ["CreateSurface"] = ("Create Surface", "Создать поверхность"),
        ["CreateConeSurface"] = ("Cone Surface", "Конусная поверхность"),
        ["SurfaceChange"] = ("Change Surface", "Изменить поверхность"),
        ["SurfaceClearLayer"] = ("Clear Surface Layer", "Очистить слой поверхн."),
        ["CreateZone"] = ("Create Zone", "Создать зону"),
        ["CreateWall"] = ("Create Wall", "Создать стену"),

        // ── Movement & Positioning ────────────────────────────
        ["Force"] = ("Push / Pull", "Толчок / Притяжение"),
        ["DoTeleport"] = ("Teleport", "Телепортация"),
        ["TeleportSource"] = ("Teleport Source", "Телепорт источника"),
        ["SwapPlaces"] = ("Swap Places", "Поменяться местами"),
        ["Knockback"] = ("Knockback", "Отброс"),

        // ── Spells & Combat ───────────────────────────────────
        ["UseSpell"] = ("Use Spell", "Применить заклинание"),
        ["UseAttack"] = ("Use Attack", "Применить атаку"),
        ["ExecuteWeaponFunctors"] = ("Weapon Functors", "Функторы оружия"),
        ["Counterspell"] = ("Counterspell", "Контрзаклинание"),
        ["BreakConcentration"] = ("Break Concentration", "Прервать концентрацию"),
        ["ResetCooldowns"] = ("Reset Cooldowns", "Сброс перезарядок"),

        // ── Summon & Spawn ────────────────────────────────────
        ["Summon"] = ("Summon", "Призвать"),
        ["SummonInInventory"] = ("Summon to Inventory", "Призвать в инвентарь"),
        ["Spawn"] = ("Spawn", "Создать существо"),
        ["SpawnInInventory"] = ("Spawn in Inventory", "Создать в инвентаре"),
        ["Unsummon"] = ("Unsummon", "Убрать призыв"),

        // ── Roll Manipulation ─────────────────────────────────
        ["AdjustRoll"] = ("Adjust Roll", "Корректировать бросок"),
        ["SetRoll"] = ("Set Roll", "Установить бросок"),
        ["SetReroll"] = ("Set Reroll", "Установить переброс"),
        ["SetAdvantage"] = ("Set Advantage", "Дать преимущество"),
        ["SetDisadvantage"] = ("Set Disadvantage", "Дать помеху"),
        ["MaximizeRoll"] = ("Maximize Roll", "Максимизировать"),
        ["SetDamageResistance"] = ("Set Resistance", "Дать устойчивость"),

        // ── Revival & State ───────────────────────────────────
        ["Resurrect"] = ("Resurrect", "Воскресить"),
        ["Stabilize"] = ("Stabilize", "Стабилизировать"),
        ["Kill"] = ("Kill", "Убить"),
        ["SwitchDeathType"] = ("Switch Death Type", "Сменить тип смерти"),

        // ── Misc ──────────────────────────────────────────────
        ["Douse"] = ("Douse Fire", "Потушить"),
        ["Pickup"] = ("Pick Up", "Подобрать"),
        ["Drop"] = ("Drop", "Уронить"),
        ["DisarmWeapon"] = ("Disarm", "Обезоружить"),
        ["DisarmAndStealWeapon"] = ("Disarm & Steal", "Обезоружить и украсть"),
        ["Unlock"] = ("Unlock", "Открыть замок"),
        ["Sabotage"] = ("Sabotage", "Саботаж"),
        ["ShortRest"] = ("Short Rest", "Короткий отдых"),
        ["ResetCombatTurn"] = ("Reset Turn", "Сбросить ход"),
        ["CreateExplosion"] = ("Explosion", "Взрыв"),
        ["FireProjectile"] = ("Fire Projectile", "Запустить снаряд"),
        ["SpawnExtraProjectiles"] = ("Extra Projectiles", "Доп. снаряды"),
        ["TriggerRandomCast"] = ("Random Cast", "Случайный каст"),
        ["CameraWait"] = ("Camera Wait", "Ждать камеру"),
        ["TutorialEvent"] = ("Tutorial Event", "Событие туториала"),
    };

    // ═══════════════════════════════════════════════════════════
    // BOOST LABELS — (English, Russian)
    // All 78+ boosts from LSLibDefinitions.xml + AMP
    // ═══════════════════════════════════════════════════════════

    public static readonly Dictionary<string, (string En, string Ru)> Boosts = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Ability & Stats ───────────────────────────────────
        ["AC"] = ("Armor Class", "Класс брони"),
        ["Ability"] = ("Ability Score", "Показатель способности"),
        ["AbilityOverrideMinimum"] = ("Min Ability", "Минимум способности"),
        ["NullifyAbilityScore"] = ("Nullify Ability", "Обнулить способность"),
        ["ACOverrideFormula"] = ("AC Formula", "Формула КБ"),
        ["AddProficiencyToAC"] = ("Prof → AC", "Мастерство → КБ"),
        ["AddProficiencyToDamage"] = ("Prof → Damage", "Мастерство → Урон"),
        ["BlockAbilityModifierFromAC"] = ("Block Mod from AC", "Блок мод. от КБ"),
        ["ProficiencyBonusOverride"] = ("Prof Override", "Заменить мастерство"),
        ["ProficiencyBonusIncrease"] = ("Prof Increase", "Увеличить мастерство"),
        ["HalveWeaponDamage"] = ("Halve Weapon Dmg", "Половина урона оружием"),

        // ── Attack & Damage ───────────────────────────────────
        ["RollBonus"] = ("Roll Bonus", "Бонус к броску"),
        ["DamageBonus"] = ("Damage Bonus", "Бонус к урону"),
        ["CharacterWeaponDamage"] = ("Extra Weapon Dmg", "Доп. урон оружием"),
        ["CharacterUnarmedDamage"] = ("Unarmed Damage", "Урон без оружия"),
        ["WeaponDamage"] = ("Weapon Damage", "Урон оружия"),
        ["WeaponEnchantment"] = ("Enchantment", "Зачарование"),
        ["WeaponAttackRollBonus"] = ("Weapon Atk Bonus", "Бонус атаки оружием"),
        ["WeaponProperty"] = ("Weapon Property", "Свойство оружия"),
        ["WeaponAttackTypeOverride"] = ("Attack Type Override", "Замена типа атаки"),
        ["WeaponDamageDieOverride"] = ("Damage Die Override", "Замена кубика урона"),
        ["WeaponDamageTypeOverride"] = ("Damage Type Override", "Замена типа урона"),
        ["WeaponAttackRollAbilityOverride"] = ("Attack Ability Override", "Замена способн. атаки"),
        ["WeaponDamageResistance"] = ("Weapon Dmg Resist", "Сопротивление оружию"),
        ["EntityThrowDamage"] = ("Throw Damage", "Урон от метания"),
        ["DamageReduction"] = ("Damage Reduction", "Снижение урона"),
        ["DamageTakenBonus"] = ("Dmg Taken Bonus", "Бонус получ. урона"),

        // ── Critical Hits ─────────────────────────────────────
        ["CriticalHit"] = ("Critical Hit", "Критический удар"),
        ["CriticalHitExtraDice"] = ("Extra Crit Dice", "Доп. кубики крита"),
        ["ReduceCriticalAttackThreshold"] = ("Lower Crit Threshold", "Снизить порог крита"),
        ["CriticalDamageOnHit"] = ("Crit Dmg on Hit", "Крит. урон при попадании"),

        // ── Resistance & Immunity ─────────────────────────────
        ["Resistance"] = ("Resistance", "Устойчивость"),
        ["StatusImmunity"] = ("Status Immunity", "Иммунитет к статусу"),
        ["IgnoreResistance"] = ("Ignore Resistance", "Игнорировать устойчивость"),
        ["IgnoreDamageThreshold"] = ("Ignore Dmg Threshold", "Игнорировать порог урона"),
        ["SpellResistance"] = ("Spell Resistance", "Устойчив. к заклинаниям"),
        ["Invulnerable"] = ("Invulnerable", "Неуязвимость"),
        ["RedirectDamage"] = ("Redirect Damage", "Перенаправить урон"),

        // ── Advantage & Rolls ─────────────────────────────────
        ["Advantage"] = ("Advantage", "Преимущество"),
        ["Disadvantage"] = ("Disadvantage", "Помеха"),
        ["Reroll"] = ("Reroll", "Переброс"),
        ["MinimumRollResult"] = ("Minimum Roll", "Мин. результат"),
        ["MaximumRollResult"] = ("Maximum Roll", "Макс. результат"),
        ["GuaranteedChanceRollOutcome"] = ("Guaranteed Roll", "Гарант. результат"),

        // ── Proficiency ───────────────────────────────────────
        ["Proficiency"] = ("Proficiency", "Владение"),
        ["ProficiencyBonus"] = ("Prof Bonus", "Бонус мастерства"),
        ["ExpertiseBonus"] = ("Expertise", "Компетентность"),
        ["Skill"] = ("Skill Bonus", "Бонус к навыку"),

        // ── Spells & Magic ────────────────────────────────────
        ["SpellSaveDC"] = ("Spell Save DC", "КС заклинаний"),
        ["UnlockSpell"] = ("Unlock Spell", "Открыть заклинание"),
        ["UnlockSpellVariant"] = ("Spell Variant", "Вариант заклинания"),
        ["UnlockInterrupt"] = ("Unlock Interrupt", "Открыть реакцию"),
        ["Savant"] = ("Savant", "Знаток школы"),
        ["BlockSpellCast"] = ("Block Spellcast", "Блок каста"),
        ["ConcentrationIgnoreDamage"] = ("Concentration Ignore", "Концентрация игнор."),
        ["UseBoosts"] = ("Use Boosts", "Применить бусты"),

        // ── Resources ─────────────────────────────────────────
        ["ActionResource"] = ("Action Resource", "Ресурс действия"),
        ["ActionResourceOverride"] = ("Resource Override", "Заменить ресурс"),
        ["ActionResourceMultiplier"] = ("Resource ×", "Множитель ресурса"),
        ["ActionResourceBlock"] = ("Block Resource", "Заблокировать ресурс"),
        ["ActionResourceConsumeMultiplier"] = ("Consume ×", "Множитель потребления"),
        ["ActionResourcePreventReduction"] = ("Prevent Reduction", "Запретить снижение"),

        // ── HP & Healing ──────────────────────────────────────
        ["IncreaseMaxHP"] = ("Increase Max HP", "Увеличить макс. ОЗ"),
        ["TemporaryHP"] = ("Temporary HP", "Временные ОЗ"),
        ["BlockRegainHP"] = ("Block Healing", "Блок исцеления"),
        ["MaximizeHealing"] = ("Maximize Healing", "Макс. исцеление"),

        // ── Movement & Physical ───────────────────────────────
        ["Initiative"] = ("Initiative", "Инициатива"),
        ["ObjectSize"] = ("Size Change", "Изменение размера"),
        ["ObjectSizeOverride"] = ("Size Override", "Замена размера"),
        ["ScaleMultiplier"] = ("Scale ×", "Масштаб ×"),
        ["Weight"] = ("Weight", "Вес"),
        ["CarryCapacityMultiplier"] = ("Carry Capacity ×", "Грузоподъёмность ×"),
        ["JumpMaxDistanceBonus"] = ("Jump Bonus", "Бонус прыжка"),
        ["JumpMaxDistanceMultiplier"] = ("Jump ×", "Множитель прыжка"),
        ["FallDamageMultiplier"] = ("Fall Dmg ×", "Урон падения ×"),
        ["IgnoreFallDamage"] = ("No Fall Damage", "Нет урона падения"),
        ["IgnoreLeaveAttackRange"] = ("No Opportunity Atk", "Нет провоцированных"),
        ["IgnorePointBlankDisadvantage"] = ("No Melee Penalty", "Нет штрафа вблизи"),
        ["IgnoreLowGroundPenalty"] = ("No Low Ground", "Нет штрафа низины"),
        ["MovementSpeedLimit"] = ("Speed Limit", "Лимит скорости"),
        ["NonLethal"] = ("Non-Lethal", "Несмертельный"),
        ["NoAOEDamageOnLand"] = ("No AoE on Land", "Нет AoE при приземл."),
        ["IgnoreSurfaceCover"] = ("Ignore Cover", "Игнорировать укрытие"),

        // ── Vision & Light ────────────────────────────────────
        ["DarkvisionRange"] = ("Darkvision", "Тёмное зрение"),
        ["DarkvisionRangeMin"] = ("Darkvision Min", "Мин. тёмного зрения"),
        ["DarkvisionRangeOverride"] = ("Darkvision Override", "Замена тёмного зрения"),
        ["SightRangeAdditive"] = ("Sight Range +", "Доп. обзор"),
        ["SightRangeMinimum"] = ("Sight Range Min", "Мин. обзор"),
        ["SightRangeMaximum"] = ("Sight Range Max", "Макс. обзор"),
        ["Invisibility"] = ("Invisibility", "Невидимость"),
        ["GameplayLight"] = ("Light", "Свет"),
        ["GameplayObscurity"] = ("Obscurity", "Затенение"),
        ["ActiveCharacterLight"] = ("Character Light", "Свет персонажа"),

        // ── Tags & Flags ──────────────────────────────────────
        ["Tag"] = ("Add Tag", "Добавить тег"),
        ["Attribute"] = ("Attribute Flag", "Флаг атрибута"),
        ["Lootable"] = ("Lootable", "Можно обыскать"),
        ["ItemReturnToOwner"] = ("Item Returns", "Предмет возвращается"),
        ["CannotBeDisarmed"] = ("Cannot Disarm", "Нельзя обезоружить"),
        ["Detach"] = ("Detach", "Отсоединить"),
        ["ConsumeItemBlock"] = ("Block Item Consume", "Блок расхода предмета"),

        // ── Combat Mode ───────────────────────────────────────
        ["DualWielding"] = ("Dual Wielding", "Двуручный бой"),
        ["TwoWeaponFighting"] = ("Two-Weapon Fighting", "Бой двумя оружиями"),
        ["MonkWeaponDamageDiceOverride"] = ("Monk Damage Die", "Кубик монаха"),
        ["UnarmedMagicalProperty"] = ("Unarmed Magical", "Магич. безоружная"),
        ["ArmorAbilityModifierCapOverride"] = ("Armor Mod Cap", "Лимит мод. брони"),
        ["ProjectileDeflect"] = ("Deflect Projectile", "Отразить снаряд"),
        ["AreaDamageEvade"] = ("Evade AoE", "Уклонение от AoE"),
        ["SourceAdvantageOnAttack"] = ("Source Advantage", "Преимущ. источника"),
        ["DownedStatus"] = ("Downed Status", "Статус поражения"),

        // ── Social / Misc ─────────────────────────────────────
        ["HiddenDuringCinematic"] = ("Hidden in Cinematic", "Скрыт в катсцене"),
        ["DialogueBlock"] = ("Block Dialogue", "Блок диалога"),
        ["BlockTravel"] = ("Block Travel", "Блок перемещения"),
        ["VoicebarkBlock"] = ("Block Voicebark", "Блок реплик"),
        ["FactionOverride"] = ("Faction Override", "Смена фракции"),
        ["AiArchetypeOverride"] = ("AI Override", "Замена ИИ"),
        ["WeightCategory"] = ("Weight Category", "Категория веса"),
        ["ModifyNumberOfTargets"] = ("Modify Targets", "Кол-во целей"),
        ["ModifyTargetRadius"] = ("Modify Radius", "Изменить радиус"),
    };

    // ═══════════════════════════════════════════════════════════
    // ENUM VALUE LABELS — (English, Russian)
    // Complete EN+RU for every enum used by functors and boosts
    // ═══════════════════════════════════════════════════════════

    // ── Abilities ─────────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> AbilityLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Strength"] = ("Strength", "Сила"),
        ["Dexterity"] = ("Dexterity", "Ловкость"),
        ["Constitution"] = ("Constitution", "Телосложение"),
        ["Intelligence"] = ("Intelligence", "Интеллект"),
        ["Wisdom"] = ("Wisdom", "Мудрость"),
        ["Charisma"] = ("Charisma", "Харизма"),
    };

    // ── Damage Types ──────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> DamageTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["All"] = ("All", "Все"),
        ["Slashing"] = ("Slashing", "Рубящий"),
        ["Piercing"] = ("Piercing", "Колющий"),
        ["Bludgeoning"] = ("Bludgeoning", "Дробящий"),
        ["Acid"] = ("Acid", "Кислота"),
        ["Thunder"] = ("Thunder", "Гром"),
        ["Necrotic"] = ("Necrotic", "Некротический"),
        ["Fire"] = ("Fire", "Огонь"),
        ["Lightning"] = ("Lightning", "Молния"),
        ["Cold"] = ("Cold", "Холод"),
        ["Psychic"] = ("Psychic", "Психический"),
        ["Poison"] = ("Poison", "Яд"),
        ["Radiant"] = ("Radiant", "Сияние"),
        ["Force"] = ("Force", "Силовой"),
        // Extended (weapon damage types)
        ["MainWeaponDamageType"] = ("Main Weapon", "Осн. оружие"),
        ["OffhandWeaponDamageType"] = ("Offhand Weapon", "Второе оружие"),
        ["MainMeleeWeaponDamageType"] = ("Main Melee", "Осн. ближнее"),
        ["OffhandMeleeWeaponDamageType"] = ("Offhand Melee", "Второе ближнее"),
        ["MainRangedWeaponDamageType"] = ("Main Ranged", "Осн. дальнее"),
        ["OffhandRangedWeaponDamageType"] = ("Offhand Ranged", "Второе дальнее"),
        ["SourceWeaponDamageType"] = ("Source Weapon", "Оружие источника"),
        ["ThrownWeaponDamageType"] = ("Thrown Weapon", "Метательное"),
    };

    // ── Resistance Flags ──────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ResistanceLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["Resistant"] = ("Resistant", "Устойчив"),
        ["Immune"] = ("Immune", "Иммунитет"),
        ["Vulnerable"] = ("Vulnerable", "Уязвим"),
        ["BelowDamageThreshold"] = ("Below Threshold", "Ниже порога"),
        ["ResistantToMagical"] = ("Resist Magical", "Устойч. к магии"),
        ["ImmuneToMagical"] = ("Immune Magical", "Иммунитет к магии"),
        ["VulnerableToMagical"] = ("Vulner. Magical", "Уязв. к магии"),
        ["ResistantToNonMagical"] = ("Resist Non-Magic", "Устойч. к немагии"),
        ["ImmuneToNonMagical"] = ("Immune Non-Magic", "Иммунитет к немагии"),
        ["VulnerableToNonMagical"] = ("Vulner. Non-Magic", "Уязв. к немагии"),
    };

    // ── Roll Types ────────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> RollTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Attack"] = ("Attack", "Атака"),
        ["MeleeWeaponAttack"] = ("Melee Weapon", "Ближн. оружие"),
        ["RangedWeaponAttack"] = ("Ranged Weapon", "Дальн. оружие"),
        ["MeleeSpellAttack"] = ("Melee Spell", "Ближн. заклинание"),
        ["RangedSpellAttack"] = ("Ranged Spell", "Дальн. заклинание"),
        ["MeleeUnarmedAttack"] = ("Melee Unarmed", "Ближн. безоружная"),
        ["RangedUnarmedAttack"] = ("Ranged Unarmed", "Дальн. безоружная"),
        ["MeleeOffHandWeaponAttack"] = ("Melee Offhand", "Ближн. вторая рука"),
        ["RangedOffHandWeaponAttack"] = ("Ranged Offhand", "Дальн. вторая рука"),
        ["SkillCheck"] = ("Skill Check", "Навык"),
        ["SavingThrow"] = ("Saving Throw", "Спасбросок"),
        ["RawAbility"] = ("Raw Ability", "Чистая способность"),
        ["Damage"] = ("Damage", "Урон"),
        ["DeathSavingThrow"] = ("Death Save", "Спасбросок смерти"),
        ["MeleeWeaponDamage"] = ("Melee Weapon Dmg", "Урон ближн. оружия"),
        ["RangedWeaponDamage"] = ("Ranged Weapon Dmg", "Урон дальн. оружия"),
        ["MeleeSpellDamage"] = ("Melee Spell Dmg", "Урон ближн. закл."),
        ["RangedSpellDamage"] = ("Ranged Spell Dmg", "Урон дальн. закл."),
        ["MeleeUnarmedDamage"] = ("Melee Unarmed Dmg", "Урон ближн. безоруж."),
        ["RangedUnarmedDamage"] = ("Ranged Unarmed Dmg", "Урон дальн. безоруж."),
    };

    // ── Attack Types ──────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> AttackTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DirectHit"] = ("Direct Hit", "Прямое попадание"),
        ["MeleeWeaponAttack"] = ("Melee Weapon", "Ближн. оружие"),
        ["RangedWeaponAttack"] = ("Ranged Weapon", "Дальн. оружие"),
        ["MeleeOffHandWeaponAttack"] = ("Melee Offhand", "Ближн. вторая рука"),
        ["RangedOffHandWeaponAttack"] = ("Ranged Offhand", "Дальн. вторая рука"),
        ["MeleeSpellAttack"] = ("Melee Spell", "Ближн. заклинание"),
        ["RangedSpellAttack"] = ("Ranged Spell", "Дальн. заклинание"),
        ["MeleeUnarmedAttack"] = ("Melee Unarmed", "Ближн. безоружная"),
        ["RangedUnarmedAttack"] = ("Ranged Unarmed", "Дальн. безоружная"),
    };

    // ── Advantage Context ─────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> AdvantageContextLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AttackRoll"] = ("Attack Roll", "Бросок атаки"),
        ["AttackTarget"] = ("Attack Target", "Цель атаки"),
        ["SavingThrow"] = ("Saving Throw", "Спасбросок"),
        ["AllSavingThrows"] = ("All Saves", "Все спасброски"),
        ["Ability"] = ("Ability Check", "Проверка способности"),
        ["AllAbilities"] = ("All Abilities", "Все способности"),
        ["Skill"] = ("Skill Check", "Навык"),
        ["AllSkills"] = ("All Skills", "Все навыки"),
        ["SourceDialogue"] = ("Dialogue", "Диалог"),
        ["DeathSavingThrow"] = ("Death Save", "Спасбросок смерти"),
        ["Concentration"] = ("Concentration", "Концентрация"),
    };

    // ── Skills ────────────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> SkillLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Athletics"] = ("Athletics", "Атлетика"),
        ["Acrobatics"] = ("Acrobatics", "Акробатика"),
        ["SleightOfHand"] = ("Sleight of Hand", "Ловкость рук"),
        ["Stealth"] = ("Stealth", "Скрытность"),
        ["Arcana"] = ("Arcana", "Магия"),
        ["History"] = ("History", "История"),
        ["Investigation"] = ("Investigation", "Расследование"),
        ["Nature"] = ("Nature", "Природа"),
        ["Religion"] = ("Religion", "Религия"),
        ["AnimalHandling"] = ("Animal Handling", "Уход за животными"),
        ["Insight"] = ("Insight", "Проницательность"),
        ["Medicine"] = ("Medicine", "Медицина"),
        ["Perception"] = ("Perception", "Восприятие"),
        ["Survival"] = ("Survival", "Выживание"),
        ["Deception"] = ("Deception", "Обман"),
        ["Intimidation"] = ("Intimidation", "Запугивание"),
        ["Performance"] = ("Performance", "Выступление"),
        ["Persuasion"] = ("Persuasion", "Убеждение"),
    };

    // ── Weapon Properties ─────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> WeaponPropertyLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["Light"] = ("Light", "Лёгкое"),
        ["Ammunition"] = ("Ammunition", "Боеприпасы"),
        ["Finesse"] = ("Finesse", "Фехтовальное"),
        ["Heavy"] = ("Heavy", "Тяжёлое"),
        ["Loading"] = ("Loading", "Заряжаемое"),
        ["Range"] = ("Range", "Дальнобойное"),
        ["Reach"] = ("Reach", "Досягаемость"),
        ["Lance"] = ("Lance", "Копьё"),
        ["Net"] = ("Net", "Сеть"),
        ["Thrown"] = ("Thrown", "Метательное"),
        ["Twohanded"] = ("Two-Handed", "Двуручное"),
        ["Versatile"] = ("Versatile", "Универсальное"),
        ["Melee"] = ("Melee", "Ближнее"),
        ["Dippable"] = ("Dippable", "Можно обмакнуть"),
        ["Torch"] = ("Torch", "Факел"),
        ["NoDualWield"] = ("No Dual Wield", "Нельзя в пару"),
        ["Magical"] = ("Magical", "Магическое"),
        ["NeedDualWieldingBoost"] = ("Needs DW Boost", "Нужен бонус парного"),
        ["NotSheathable"] = ("Not Sheathable", "Не убирается"),
        ["Unstowable"] = ("Unstowable", "Не складывается"),
        ["AddToHotbar"] = ("Add to Hotbar", "На панель быстр."),
    };

    // ── Armor Types ───────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ArmorTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["Cloth"] = ("Cloth", "Ткань"),
        ["Padded"] = ("Padded", "Стёганая"),
        ["Leather"] = ("Leather", "Кожаная"),
        ["StuddedLeather"] = ("Studded Leather", "Проклёп. кожаная"),
        ["Hide"] = ("Hide", "Шкурная"),
        ["ChainShirt"] = ("Chain Shirt", "Кольчужная рубаха"),
        ["ScaleMail"] = ("Scale Mail", "Чешуйчатый"),
        ["BreastPlate"] = ("Breastplate", "Кираса"),
        ["HalfPlate"] = ("Half Plate", "Полулаты"),
        ["RingMail"] = ("Ring Mail", "Кольчатый"),
        ["ChainMail"] = ("Chain Mail", "Кольчуга"),
        ["Splint"] = ("Splint", "Наборный"),
        ["Plate"] = ("Plate", "Латы"),
    };

    // ── Equipment Slots ───────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> SlotLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Helmet"] = ("Helmet", "Шлем"),
        ["Breast"] = ("Chest", "Нагрудник"),
        ["Cloak"] = ("Cloak", "Плащ"),
        ["MeleeMainHand"] = ("Melee Main", "Ближнее основное"),
        ["MeleeOffHand"] = ("Melee Off", "Ближнее второе"),
        ["RangedMainHand"] = ("Ranged Main", "Дальнее основное"),
        ["RangedOffHand"] = ("Ranged Off", "Дальнее второе"),
        ["Ring"] = ("Ring", "Кольцо"),
        ["Ring2"] = ("Ring 2", "Кольцо 2"),
        ["Underwear"] = ("Underwear", "Бельё"),
        ["Boots"] = ("Boots", "Сапоги"),
        ["Gloves"] = ("Gloves", "Перчатки"),
        ["Amulet"] = ("Amulet", "Амулет"),
        ["Wings"] = ("Wings", "Крылья"),
        ["Horns"] = ("Horns", "Рога"),
        ["Overhead"] = ("Overhead", "Надголовный"),
        ["MusicalInstrument"] = ("Instrument", "Инструмент"),
        ["VanityBody"] = ("Vanity Body", "Косметика (тело)"),
        ["VanityBoots"] = ("Vanity Boots", "Косметика (сапоги)"),
        ["MainHand"] = ("Main Hand", "Основная рука"),
        ["OffHand"] = ("Off Hand", "Вторая рука"),
    };

    // ── Spell Schools ─────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> SpellSchoolLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["Abjuration"] = ("Abjuration", "Ограждение"),
        ["Conjuration"] = ("Conjuration", "Вызов"),
        ["Divination"] = ("Divination", "Прорицание"),
        ["Enchantment"] = ("Enchantment", "Очарование"),
        ["Evocation"] = ("Evocation", "Воплощение"),
        ["Illusion"] = ("Illusion", "Иллюзия"),
        ["Necromancy"] = ("Necromancy", "Некромантия"),
        ["Transmutation"] = ("Transmutation", "Преобразование"),
    };

    // ── Proficiency Groups ────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ProficiencyLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LightArmor"] = ("Light Armor", "Лёгкая броня"),
        ["MediumArmor"] = ("Medium Armor", "Средняя броня"),
        ["HeavyArmor"] = ("Heavy Armor", "Тяжёлая броня"),
        ["Shields"] = ("Shields", "Щиты"),
        ["SimpleMeleeWeapon"] = ("Simple Melee", "Простое ближнее"),
        ["SimpleRangedWeapon"] = ("Simple Ranged", "Простое дальнее"),
        ["MartialMeleeWeapon"] = ("Martial Melee", "Воинское ближнее"),
        ["MartialRangedWeapon"] = ("Martial Ranged", "Воинское дальнее"),
        ["HandCrossbows"] = ("Hand Crossbow", "Ручной арбалет"),
        ["Battleaxes"] = ("Battleaxe", "Боевой топор"),
        ["Flails"] = ("Flail", "Цеп"),
        ["Glaives"] = ("Glaive", "Глефа"),
        ["Greataxes"] = ("Greataxe", "Секира"),
        ["Greatswords"] = ("Greatsword", "Двуручный меч"),
        ["Halberds"] = ("Halberd", "Алебарда"),
        ["Longswords"] = ("Longsword", "Длинный меч"),
        ["Mauls"] = ("Maul", "Молот"),
        ["Morningstars"] = ("Morningstar", "Моргенштерн"),
        ["Pikes"] = ("Pike", "Пика"),
        ["Rapiers"] = ("Rapier", "Рапира"),
        ["Scimitars"] = ("Scimitar", "Скимитар"),
        ["Shortswords"] = ("Shortsword", "Короткий меч"),
        ["Tridents"] = ("Trident", "Трезубец"),
        ["WarPicks"] = ("War Pick", "Боевой клевец"),
        ["Warhammers"] = ("Warhammer", "Боевой молот"),
        ["Clubs"] = ("Club", "Дубина"),
        ["Daggers"] = ("Dagger", "Кинжал"),
        ["Greatclubs"] = ("Greatclub", "Палица"),
        ["Handaxes"] = ("Handaxe", "Ручной топор"),
        ["Javelins"] = ("Javelin", "Метательное копьё"),
        ["LightHammers"] = ("Light Hammer", "Лёгкий молот"),
        ["Maces"] = ("Mace", "Булава"),
        ["Quarterstaffs"] = ("Quarterstaff", "Посох"),
        ["Sickles"] = ("Sickle", "Серп"),
        ["Spears"] = ("Spear", "Копьё"),
        ["LightCrossbows"] = ("Light Crossbow", "Лёгкий арбалет"),
        ["Darts"] = ("Dart", "Дротик"),
        ["Shortbows"] = ("Shortbow", "Короткий лук"),
        ["Slings"] = ("Sling", "Праща"),
        ["Longbows"] = ("Longbow", "Длинный лук"),
        ["HeavyCrossbows"] = ("Heavy Crossbow", "Тяжёлый арбалет"),
        ["MusicalInstrument"] = ("Musical Instrument", "Муз. инструмент"),
    };

    // ── Action Resources ──────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ActionResourceLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ActionPoint"] = ("Action", "Действие"),
        ["BonusActionPoint"] = ("Bonus Action", "Бонусное действие"),
        ["Movement"] = ("Movement", "Перемещение"),
        ["SpellSlot"] = ("Spell Slot", "Ячейка заклинания"),
        ["KiPoint"] = ("Ki Point", "Очко ки"),
        ["Rage"] = ("Rage", "Ярость"),
        ["SorceryPoint"] = ("Sorcery Point", "Очко колдовства"),
        ["BardicInspiration"] = ("Bardic Inspiration", "Вдохновение барда"),
        ["SuperiorityDie"] = ("Superiority Die", "Кубик превосходства"),
        ["ChannelDivinity"] = ("Channel Divinity", "Божественный канал"),
        ["LayOnHandsCharge"] = ("Lay on Hands", "Наложение рук"),
        ["WildShape"] = ("Wild Shape", "Дикий облик"),
        ["NaturalRecovery"] = ("Natural Recovery", "Естеств. восстановление"),
        ["ChannelOath"] = ("Channel Oath", "Канал клятвы"),
        ["WarlockSpellSlot"] = ("Warlock Slot", "Ячейка варлока"),
        ["ArcaneRecovery"] = ("Arcane Recovery", "Магическое восстановл."),
        ["FungalInfestation"] = ("Fungal Infestation", "Грибковое заражение"),
        ["SneakAttack"] = ("Sneak Attack", "Скрытая атака"),
    };

    // ── Surface Types (for CreateSurface / SurfaceChange) ─────
    public static readonly Dictionary<string, (string En, string Ru)> SurfaceTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["Water"] = ("Water", "Вода"),
        ["WaterElectrified"] = ("Electr. Water", "Электр. вода"),
        ["WaterFrozen"] = ("Frozen Water", "Лёд"),
        ["Blood"] = ("Blood", "Кровь"),
        ["BloodElectrified"] = ("Electr. Blood", "Электр. кровь"),
        ["BloodFrozen"] = ("Frozen Blood", "Замёрзшая кровь"),
        ["Poison"] = ("Poison", "Яд"),
        ["Oil"] = ("Oil", "Масло"),
        ["Lava"] = ("Lava", "Лава"),
        ["Grease"] = ("Grease", "Жир"),
        ["Web"] = ("Web", "Паутина"),
        ["Deepwater"] = ("Deep Water", "Глубокая вода"),
        ["Vines"] = ("Vines", "Лозы"),
        ["Fire"] = ("Fire", "Огонь"),
        ["Acid"] = ("Acid", "Кислота"),
        ["Mud"] = ("Mud", "Грязь"),
        ["Alcohol"] = ("Alcohol", "Алкоголь"),
    };

    // ── Surface Change ────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> SurfaceChangeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["Ignite"] = ("Ignite", "Поджечь"),
        ["Douse"] = ("Douse", "Потушить"),
        ["Electrify"] = ("Electrify", "Электрифицировать"),
        ["Deelectrify"] = ("De-electrify", "Снять электричество"),
        ["Freeze"] = ("Freeze", "Заморозить"),
        ["Melt"] = ("Melt", "Растопить"),
        ["Vaporize"] = ("Vaporize", "Испарить"),
        ["Condense"] = ("Condense", "Конденсировать"),
        ["DestroyWater"] = ("Destroy Water", "Уничтожить воду"),
        ["Clear"] = ("Clear", "Очистить"),
    };

    // ── Surface Layer ─────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> SurfaceLayerLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ground"] = ("Ground", "Земля"),
        ["Cloud"] = ("Cloud", "Облако"),
    };

    // ── Resurrect Types ───────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ResurrectTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Living"] = ("Living", "Живой"),
        ["Guaranteed"] = ("Guaranteed", "Гарантированно"),
        ["Construct"] = ("Construct", "Конструкт"),
        ["Undead"] = ("Undead", "Нежить"),
    };

    // ── Death Types ───────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> DeathTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["Acid"] = ("Acid", "Кислота"),
        ["Chasm"] = ("Chasm", "Пропасть"),
        ["DoT"] = ("DoT", "Периодический"),
        ["Electrocution"] = ("Electrocution", "Электрошок"),
        ["Explode"] = ("Explode", "Взрыв"),
        ["Falling"] = ("Falling", "Падение"),
        ["Incinerate"] = ("Incinerate", "Сожжение"),
        ["KnockedDown"] = ("Knocked Down", "Повален"),
        ["Lifetime"] = ("Lifetime", "Срок жизни"),
        ["Narcolepsy"] = ("Narcolepsy", "Нарколепсия"),
        ["PetrifiedShattered"] = ("Petrified Shattered", "Окаменение"),
        ["Sentinel"] = ("Sentinel", "Страж"),
    };

    // ── Spell Cooldown Types ──────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> CooldownLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Default"] = ("Default", "По умолчанию"),
        ["OncePerTurn"] = ("Once per Turn", "Раз в ход"),
        ["OncePerCombat"] = ("Once per Combat", "Раз в бой"),
        ["UntilRest"] = ("Until Rest", "До отдыха"),
        ["OncePerTurnNoRealtime"] = ("Once/Turn (no RT)", "Раз/ход (без РВ)"),
        ["UntilShortRest"] = ("Until Short Rest", "До корот. отдыха"),
        ["UntilPerRestPerItem"] = ("Per Rest per Item", "За отдых за предмет"),
        ["OncePerShortRestPerItem"] = ("Per Short Rest/Item", "За кор. отдых/предмет"),
    };

    // ── Set Status Duration Type ──────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> DurationChangeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SetMinimum"] = ("Set Minimum", "Установить минимум"),
        ["ForceSet"] = ("Force Set", "Принудительно"),
        ["Add"] = ("Add", "Добавить"),
        ["Multiply"] = ("Multiply", "Умножить"),
    };

    // ── Force Functor Origin ──────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ForceOriginLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OriginToEntity"] = ("Origin → Entity", "Начало → Существо"),
        ["OriginToTarget"] = ("Origin → Target", "Начало → Цель"),
        ["TargetToEntity"] = ("Target → Entity", "Цель → Существо"),
    };

    // ── Force Functor Aggression ──────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ForceAggressionLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Aggressive"] = ("Push Away", "Оттолкнуть"),
        ["Friendly"] = ("Pull Toward", "Притянуть"),
        ["Neutral"] = ("Neutral", "Нейтрально"),
    };

    // ── Execute Weapon Functors Type ──────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> WeaponHandLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MainHand"] = ("Main Hand", "Основная рука"),
        ["OffHand"] = ("Off Hand", "Вторая рука"),
        ["BothHands"] = ("Both Hands", "Обе руки"),
    };

    // ── Unlock Spell Types ────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> UnlockSpellTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Singular"] = ("Single Spell", "Одно заклинание"),
        ["AddChildren"] = ("Add Children", "Добавить подвиды"),
        ["MostPowerful"] = ("Most Powerful", "Самое мощное"),
    };

    // ── Summon Duration ───────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> SummonDurationLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UntilLongRest"] = ("Until Long Rest", "До длинного отдыха"),
        ["Permanent"] = ("Permanent", "Навсегда"),
    };

    // ── Critical Hit enums ────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> CriticalHitTypeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AttackTarget"] = ("On Target", "По цели"),
        ["AttackRoll"] = ("On Roll", "По броску"),
    };

    public static readonly Dictionary<string, (string En, string Ru)> CriticalHitWhenLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Never"] = ("Never", "Никогда"),
        ["Always"] = ("Always", "Всегда"),
        ["ForcedAlways"] = ("Forced Always", "Принудительно"),
    };

    // ── Roll Adjustment ───────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> RollAdjustmentLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["All"] = ("All Dice", "Все кубики"),
        ["Distribute"] = ("Distribute", "Распределить"),
    };

    // ── Damage Reduction Type ─────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> DamageReductionLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Half"] = ("Half", "Половина"),
        ["Flat"] = ("Flat", "Фиксированное"),
        ["Threshold"] = ("Threshold", "Порог"),
    };

    // ── Healing Direction ─────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> HealingDirectionLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Incoming"] = ("Incoming", "Входящее"),
        ["Outgoing"] = ("Outgoing", "Исходящее"),
    };

    // ── Movement Speed Type ───────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> MovementSpeedLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Stroll"] = ("Stroll", "Прогулка"),
        ["Walk"] = ("Walk", "Шаг"),
        ["Run"] = ("Run", "Бег"),
        ["Sprint"] = ("Sprint", "Спринт"),
    };

    // ── Zone Shape ────────────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ZoneShapeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Cone"] = ("Cone", "Конус"),
        ["Square"] = ("Square", "Квадрат"),
    };

    // ── Attribute Flags ───────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> AttributeFlagLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = ("None", "Нет"),
        ["SlippingImmunity"] = ("No Slip", "Не скользит"),
        ["Torch"] = ("Torch", "Факел"),
        ["Arrow"] = ("Arrow", "Стрела"),
        ["Unbreakable"] = ("Unbreakable", "Неломаемый"),
        ["Grounded"] = ("Grounded", "Заземлён"),
        ["Floating"] = ("Floating", "Парит"),
        ["InventoryBound"] = ("Inventory Bound", "Привязан к инвентарю"),
        ["IgnoreClouds"] = ("Ignore Clouds", "Игнорирует облака"),
        ["BackstabImmunity"] = ("No Backstab", "Нет удара в спину"),
        ["ThrownImmunity"] = ("No Throw", "Нельзя бросить"),
        ["InvisibilityImmunity"] = ("No Invisibility", "Нет невидимости"),
    };

    // ── Magical / Nonlethal (DealDamage flags) ────────────────
    public static readonly Dictionary<string, (string En, string Ru)> MagicalLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Magical"] = ("Magical", "Магический"),
        ["Nonmagical"] = ("Non-Magical", "Немагический"),
    };

    public static readonly Dictionary<string, (string En, string Ru)> NonlethalLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Lethal"] = ("Lethal", "Смертельный"),
        ["Nonlethal"] = ("Non-Lethal", "Несмертельный"),
    };

    // ── Trigger Events (StatsFunctorContext) ──────────────────
    public static readonly Dictionary<string, (string En, string Ru)> TriggerEventLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OnTurn"] = ("Each Turn", "Каждый ход"),
        ["OnSpellCast"] = ("On Spell Cast", "При касте"),
        ["OnAttack"] = ("On Attack", "При атаке"),
        ["OnAttacked"] = ("On Attacked", "При атаке на вас"),
        ["OnApply"] = ("On Apply", "При наложении"),
        ["OnRemove"] = ("On Remove", "При снятии"),
        ["OnApplyAndTurn"] = ("On Apply + Turn", "При наложении и ходе"),
        ["OnDamage"] = ("On Damage", "При нанесении урона"),
        ["OnEquip"] = ("On Equip", "При экипировке"),
        ["OnUnequip"] = ("On Unequip", "При снятии экип."),
        ["OnHeal"] = ("On Heal", "При исцелении"),
        ["OnObscurityChanged"] = ("Visibility Change", "Смена видимости"),
        ["OnSurfaceEnter"] = ("Surface Enter", "Вход на поверхность"),
        ["OnStatusApplied"] = ("Status Applied", "Статус наложен"),
        ["OnStatusRemoved"] = ("Status Removed", "Статус снят"),
        ["OnMove"] = ("On Move", "При перемещении"),
        ["OnCombatEnded"] = ("Combat Ended", "Конец боя"),
        ["OnLockpickingSucceeded"] = ("Lockpick OK", "Успех взлома"),
        ["OnSourceDeath"] = ("Source Death", "Смерть источника"),
        ["OnFactionChanged"] = ("Faction Changed", "Смена фракции"),
        ["OnEntityPickUp"] = ("Pick Up", "Подобрать"),
        ["OnEntityDrop"] = ("Drop", "Уронить"),
        ["OnCreate"] = ("On Create", "При создании"),
    };

    // ── Scope Modifiers ───────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> ScopeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SELF"] = ("on Self", "на себя"),
        ["SWAP"] = ("on Attacker", "на атакующего"),
        ["OBSERVER_SOURCE"] = ("on Observer", "на наблюдателя"),
        ["GROUND"] = ("on Ground", "на поверхность"),
    };

    // ── Size Categories ───────────────────────────────────────
    public static readonly Dictionary<string, (string En, string Ru)> SizeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tiny"] = ("Tiny", "Крошечный"),
        ["Small"] = ("Small", "Маленький"),
        ["Medium"] = ("Medium", "Средний"),
        ["Large"] = ("Large", "Большой"),
        ["Huge"] = ("Huge", "Огромный"),
        ["Gargantuan"] = ("Gargantuan", "Громадный"),
    };

    // ═══════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════

    /// <summary>Get localized functor label. Falls back to raw name.</summary>
    public static string GetFunctorLabel(string name, bool russian = false)
    {
        if (Functors.TryGetValue(name, out var label))
            return russian ? label.Ru : label.En;
        return name;
    }

    /// <summary>Get localized boost label. Falls back to raw name.</summary>
    public static string GetBoostLabel(string name, bool russian = false)
    {
        if (Boosts.TryGetValue(name, out var label))
            return russian ? label.Ru : label.En;
        return name;
    }

    /// <summary>
    /// Get localized enum value label from any enum dictionary.
    /// Tries all known enum label dictionaries in order.
    /// </summary>
    public static string GetEnumLabel(string value, bool russian = false)
    {
        if (TryGetFromAny(value, russian, out var result))
            return result;
        return value;
    }

    /// <summary>
    /// Get localized enum value from a specific dictionary.
    /// </summary>
    public static string GetEnumLabel(string value, Dictionary<string, (string En, string Ru)> dict, bool russian = false)
    {
        if (dict.TryGetValue(value, out var label))
            return russian ? label.Ru : label.En;
        return value;
    }

    private static bool TryGetFromAny(string value, bool ru, out string result)
    {
        Dictionary<string, (string En, string Ru)>[] all =
        [
            DamageTypeLabels, AbilityLabels, SkillLabels, RollTypeLabels,
            AttackTypeLabels, AdvantageContextLabels, ResistanceLabels,
            WeaponPropertyLabels, ArmorTypeLabels, SlotLabels, SpellSchoolLabels,
            ProficiencyLabels, ActionResourceLabels, SurfaceTypeLabels,
            SurfaceChangeLabels, AttributeFlagLabels, CooldownLabels,
            ForceOriginLabels, ForceAggressionLabels, WeaponHandLabels,
            DurationChangeLabels, ResurrectTypeLabels, DeathTypeLabels,
            MagicalLabels, NonlethalLabels, SizeLabels,
            HealingDirectionLabels, MovementSpeedLabels, ZoneShapeLabels,
            RollAdjustmentLabels, DamageReductionLabels,
            CriticalHitTypeLabels, CriticalHitWhenLabels,
            UnlockSpellTypeLabels, SummonDurationLabels,
            TriggerEventLabels, ScopeLabels,
        ];
        foreach (var dict in all)
        {
            if (dict.TryGetValue(value, out var label))
            {
                result = ru ? label.Ru : label.En;
                return true;
            }
        }
        result = value;
        return false;
    }
}
