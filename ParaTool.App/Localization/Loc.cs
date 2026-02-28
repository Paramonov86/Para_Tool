using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParaTool.App.Localization;

public partial class Loc : ObservableObject
{
    public static Loc Instance { get; } = new();

    [ObservableProperty] private string _lang = "ru";

    public bool IsRussian => Lang == "ru";

    public void SetLanguage(string lang)
    {
        Lang = lang;
        OnPropertyChanged(string.Empty); // Refresh all bindings
    }

    // Startup
    public string AppTitle => "ParaTool";
    public string ErrorModsNotFound => IsRussian
        ? "Папка Mods в AppData не найдена! Укажите путь вручную"
        : "Mods folder not found in AppData! Select path manually";
    public string SelectFolder => IsRussian ? "Выбрать папку" : "Select folder";

    // Scanning
    public string PleaseWait => IsRussian ? "Подождите..." : "Please wait...";
    public string ScanningMods => IsRussian
        ? "Идёт анализ модов с предметами."
        : "Scanning mods with items.";
    public string MayTakeTime => IsRussian
        ? "Это может занять некоторое время..."
        : "This may take some time...";
    public string ModsScanned => IsRussian ? "Модов просканировано:" : "Mods scanned:";
    public string ModsSelected => IsRussian ? "Модов выбрано:" : "Mods selected:";

    // Editor
    public string EditorTitle => IsRussian
        ? "Уточните предметы для интеграции"
        : "Review items for integration";
    public string AllMods => IsRussian ? "Все моды" : "All mods";
    public string Instructions => IsRussian ? "Инструкция" : "Instructions";
    public string InstructionStep1 => IsRussian
        ? "1. Выберите, какие предметы вы хотите интегрировать в лут-листы Ancient Mega Pack."
        : "1. Select which items you want to integrate into Ancient Mega Pack loot tables.";
    public string InstructionStep2 => IsRussian
        ? "2. Поменяйте по желанию их редкость и слот"
        : "2. Optionally change their rarity and slot";
    public string InstructionStep3 => IsRussian
        ? "3. Выберите тематики"
        : "3. Select themes";
    public string InstructionStep4 => IsRussian
        ? "4. Подождите, пока приложение сделает свою работу"
        : "4. Wait for the app to finish";
    public string PatchButton => IsRussian ? "ПРОПАТЧИТЬ" : "PATCH";
    public string ModsFoundInfo(int count) => IsRussian
        ? $"В папке Mods найдено {count} модов, содержащих предметы."
        : $"Found {count} mods with items in Mods folder.";
    public string Russian => "Русский";
    public string English => "English";

    // Patching
    public string PatchingInProgress => IsRussian ? "Идёт патч..." : "Patching...";
    public string PatchSuccess => IsRussian ? "Успешно!" : "Success!";
    public string PatchError => IsRussian ? "Ошибка:" : "Error:";

    // Themes
    public string ThemeSwamp => IsRussian ? "Болото" : "Swamp";
    public string ThemeAquatic => IsRussian ? "Вода" : "Aquatic";
    public string ThemeShadowfell => IsRussian ? "Тень" : "Shadowfell";
    public string ThemeArcane => IsRussian ? "Магия" : "Arcane";
    public string ThemeCelestial => IsRussian ? "Свет" : "Celestial";
    public string ThemeNature => IsRussian ? "Природа" : "Nature";
    public string ThemeDestructive => IsRussian ? "Разрушение" : "Destructive";
    public string ThemeWar => IsRussian ? "Война" : "War";
    public string ThemePsionic => IsRussian ? "Псионика" : "Psionic";
    public string ThemePrimal => IsRussian ? "Первобытное" : "Primal";

    // Rarity display
    public string RarityUncommon => IsRussian ? "Необыч." : "Uncommon";
    public string RarityRare => IsRussian ? "Редк." : "Rare";
    public string RarityVeryRare => IsRussian ? "Оч.редк." : "Very Rare";
    public string RarityLegendary => IsRussian ? "Легендарн." : "Legendary";
}
