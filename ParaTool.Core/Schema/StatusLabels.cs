namespace ParaTool.Core.Schema;

/// <summary>
/// Readable names for the enum values a status card edits (status type, stack type, tick type,
/// property flags, remove events). English and Russian; other languages show the English one.
/// A value missing here is shown as the game writes it.
/// </summary>
public static class StatusLabels
{
    public static string Get(string value, string lang) =>
        Map.TryGetValue(value, out var pair) ? (lang == "ru" ? pair.ru : pair.en) : value;

    public static string[] For(IEnumerable<string> values, string lang) => values.Select(v => Get(v, lang)).ToArray();

    private static readonly Dictionary<string, (string en, string ru)> Map = new(StringComparer.Ordinal)
    {
        // StatusType
        ["BOOST"] = ("Boost", "Бонус"),
        ["EFFECT"] = ("Visual effect", "Визуальный эффект"),
        ["INCAPACITATED"] = ("Incapacitated", "Недееспособность"),
        ["INVISIBLE"] = ("Invisible", "Невидимость"),
        ["FEAR"] = ("Frightened", "Испуг"),
        ["KNOCKED_DOWN"] = ("Knocked down", "Сбит с ног"),
        ["POLYMORPHED"] = ("Polymorphed", "Превращение"),
        ["DOWNED"] = ("Downed", "При смерти"),
        ["HEAL"] = ("Heal", "Лечение"),
        ["SNEAKING"] = ("Sneaking", "Скрытность"),
        ["DEACTIVATED"] = ("Deactivated", "Отключён"),

        // StatusStackType
        ["Overwrite"] = ("Overwrite", "Перезапись"),
        ["Stack"] = ("Stack", "Стак"),
        ["Ignore"] = ("Ignore new", "Игнорировать новый"),
        ["Additive"] = ("Add duration", "Суммировать длительность"),
        ["Deactivate"] = ("Deactivate", "Деактивация"),
        ["Variable"] = ("Variable", "Переменный"),

        // TickType
        ["StartTurn"] = ("Start of turn", "В начале хода"),
        ["EndTurn"] = ("End of turn", "В конце хода"),
        ["StartRound"] = ("Start of round", "В начале раунда"),
        ["EndRound"] = ("End of round", "В конце раунда"),

        // StatusEvent (RemoveEvents)
        ["OnTurn"] = ("Turn passes", "Проходит ход"),
        ["OnSpellCast"] = ("Casts a spell", "Применяет заклинание"),
        ["OnAttack"] = ("Attacks", "Атакует"),
        ["OnAttacked"] = ("Is attacked", "Атакован"),
        ["OnApply"] = ("Applied", "Наложен"),
        ["OnRemove"] = ("Removed", "Снят"),
        ["OnApplyAndTurn"] = ("Applied or turn passes", "Наложен или прошёл ход"),
        ["OnDamage"] = ("Takes damage", "Получает урон"),
        ["OnEquip"] = ("Equips", "Экипирует"),
        ["OnUnequip"] = ("Unequips", "Снимает предмет"),
        ["OnHeal"] = ("Is healed", "Исцелён"),
        ["OnObscurityChanged"] = ("Obscurity changes", "Меняется скрытность"),
        ["OnSurfaceEnter"] = ("Enters a surface", "Входит в поверхность"),
        ["OnStatusApplied"] = ("Gets a status", "Получает статус"),
        ["OnStatusRemoved"] = ("Loses a status", "Теряет статус"),
        ["OnMove"] = ("Moves", "Двигается"),
        ["OnCombatEnded"] = ("Combat ends", "Бой заканчивается"),
        ["OnSourceDeath"] = ("Source dies", "Источник погибает"),
        ["OnSourceStatusApplied"] = ("Source gets a status", "Источник получает статус"),
        ["OnFactionChanged"] = ("Faction changes", "Меняется фракция"),

        // StatusPropertyFlags
        ["DisableOverhead"] = ("No overhead text", "Без надписи над головой"),
        ["DisableCombatlog"] = ("Not in combat log", "Без записи в журнале боя"),
        ["DisablePortraitIndicator"] = ("No portrait icon", "Без значка на портрете"),
        ["DisableImmunityOverhead"] = ("No immunity text", "Без надписи иммунитета"),
        ["ForceOverhead"] = ("Always overhead text", "Всегда надпись над головой"),
        ["OverheadOnTurn"] = ("Overhead text each turn", "Надпись каждый ход"),
        ["IgnoreResting"] = ("Survives resting", "Не снимается отдыхом"),
        ["ApplyToDead"] = ("Applies to the dead", "Действует на мёртвых"),
        ["IsInvulnerable"] = ("Invulnerable", "Неуязвимость"),
        ["IsInvulnerableVisible"] = ("Invulnerable (shown)", "Неуязвимость (видимая)"),
        ["LoseControl"] = ("Loses control", "Потеря контроля"),
        ["LoseControlFriendly"] = ("Loses control (friendly)", "Потеря контроля (дружеская)"),
        ["InitiateCombat"] = ("Starts combat", "Начинает бой"),
        ["BringIntoCombat"] = ("Pulls into combat", "Втягивает в бой"),
        ["AllowLeaveCombat"] = ("Can leave combat", "Можно выйти из боя"),
        ["PeaceOnly"] = ("Out of combat only", "Только вне боя"),
        ["Toggle"] = ("Toggle", "Переключаемый"),
        ["IsChanneled"] = ("Channelled", "Поддерживаемый"),
        ["MultiplyEffectsByDuration"] = ("Effects × duration", "Эффекты × длительность"),
        ["TickingWithSource"] = ("Ticks on the source's turn", "Тикает в ход источника"),
        ["FreezeDuration"] = ("Duration frozen", "Длительность заморожена"),
        ["NonExtendable"] = ("Cannot be extended", "Нельзя продлить"),
        ["ExecuteFunctorsOnOwner"] = ("Effects on owner", "Эффекты на владельца"),
        ["Blind"] = ("Blinds", "Ослепляет"),
        ["Burning"] = ("Burning", "Горение"),
        ["GiveExp"] = ("Gives experience", "Даёт опыт"),
        ["Performing"] = ("Performing", "Выступление"),
        ["DisableInteractions"] = ("No interactions", "Без взаимодействий"),
        ["IgnoredByImmobilized"] = ("Ignored when immobile", "Игнорируется обездвиженным"),
        ["UnavailableInActiveRoll"] = ("Not in active rolls", "Не в активных бросках"),
        ["IndicateDarkness"] = ("Shows darkness", "Показывает тьму"),
        ["ForceNeutralInteractions"] = ("Neutral interactions", "Нейтральные взаимодействия"),
        ["ExcludeFromPortraitRendering"] = ("Not on portrait", "Не на портрете"),
    };
}
