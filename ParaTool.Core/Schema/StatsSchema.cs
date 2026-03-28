using System.Reflection;
using System.Text.RegularExpressions;

namespace ParaTool.Core.Schema;

/// <summary>
/// Describes one field (modifier) within a stats type.
/// </summary>
public sealed class FieldDef
{
    public required string Name { get; init; }
    public required string ValueType { get; init; }  // "ConstantInt", "FixedString", "Conditions", etc.

    /// <summary>True if this is a numeric field (ConstantInt, ConstantFloat).</summary>
    public bool IsNumeric => ValueType is "ConstantInt" or "ConstantFloat" or "Qualifier";

    /// <summary>True if this is a free-text field.</summary>
    public bool IsFreeText => ValueType is "FixedString" or "Conditions" or "TargetConditions"
        or "StatsFunctors" or "RollConditions" or "StatusIDs" or "Requirements"
        or "MemorizationRequirements";

    /// <summary>True if this field references a ValueList enum.</summary>
    public bool IsEnum => !IsNumeric && !IsFreeText
        && ValueType != "TranslatedString" && ValueType != "Guid"
        && ValueType != "TreasureSubtables" && ValueType != "TreasureSubtableObject"
        && ValueType != "TreasureDrop" && ValueType != "Passthrough";

    /// <summary>True if this is a localized string field (DisplayName, Description).</summary>
    public bool IsTranslatedString => ValueType == "TranslatedString";

    /// <summary>True if this is a flags field (can have multiple values separated by ;).</summary>
    public bool IsFlags => ValueType.EndsWith("Flags") || ValueType.EndsWith("FlagList")
        || ValueType == "WeaponFlags" || ValueType == "StatusEvent";
}

/// <summary>
/// Describes a complete stats type (Armor, Weapon, PassiveData, StatusData, SpellData, etc.).
/// </summary>
public sealed class TypeDef
{
    public required string Name { get; init; }
    public required List<FieldDef> Fields { get; init; }

    /// <summary>Get a field by name (case-insensitive).</summary>
    public FieldDef? GetField(string name) =>
        Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Get all field names.</summary>
    public IEnumerable<string> FieldNames => Fields.Select(f => f.Name);
}

/// <summary>
/// A named list of allowed values (from ValueLists.txt).
/// </summary>
public sealed class ValueList
{
    public required string Name { get; init; }
    public required List<string> Values { get; init; }
}

/// <summary>
/// A condition/function available for Conditions fields (from .khn and .lua files).
/// </summary>
public sealed class ConditionFunc
{
    public required string Name { get; init; }
    public string? Signature { get; init; }  // e.g. "ManeuverSaveDC()" or "HasStatus(statusId, entity)"
    public required string Source { get; init; }  // "Vanilla", "AMP", "Hardcoded"
}

/// <summary>
/// Complete BG3 stats schema -- loaded once from embedded resources.
/// Provides field definitions, value lists, and condition autocomplete for the constructor.
/// </summary>
public sealed class StatsSchema
{
    private static StatsSchema? _instance;
    private static readonly object _lock = new();

    public Dictionary<string, TypeDef> Types { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, ValueList> ValueLists { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ConditionFunc> Conditions { get; } = [];

    /// <summary>Singleton instance, loaded lazily from embedded resources.</summary>
    public static StatsSchema Instance
    {
        get
        {
            if (_instance == null)
                lock (_lock)
                    _instance ??= Load();
            return _instance;
        }
    }

    /// <summary>Get type definition by name.</summary>
    public TypeDef? GetType(string name) =>
        Types.TryGetValue(name, out var t) ? t : null;

    /// <summary>Get allowed values for a value type (enum field).</summary>
    public ValueList? GetValueList(string name) =>
        ValueLists.TryGetValue(name, out var v) ? v : null;

    /// <summary>Get allowed values for a specific field in a specific type.</summary>
    public List<string>? GetAllowedValues(string typeName, string fieldName)
    {
        var type = GetType(typeName);
        var field = type?.GetField(fieldName);
        if (field == null || !field.IsEnum) return null;
        return GetValueList(field.ValueType)?.Values;
    }

    /// <summary>Search conditions by prefix (for autocomplete).</summary>
    public List<ConditionFunc> SearchConditions(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return Conditions;
        return Conditions.Where(c =>
            c.Name.Contains(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    // --- Parsing -----------------------------------------

    private static StatsSchema Load()
    {
        var schema = new StatsSchema();

        var assembly = Assembly.GetExecutingAssembly();

        // Parse Modifiers.txt -> Types
        var modText = ReadResource(assembly, "ParaTool.Core.Resources.Schema.Modifiers.txt");
        ParseModifiers(modText, schema);

        // Parse ValueLists.txt -> ValueLists
        var vlText = ReadResource(assembly, "ParaTool.Core.Resources.Schema.ValueLists.txt");
        ParseValueLists(vlText, schema);

        // Parse condition files -> Conditions
        var hardcoded = ReadResource(assembly, "ParaTool.Core.Resources.Schema.Khn.HardcodedConditions.lua");
        ParseConditions(hardcoded, "Hardcoded", schema);

        var common = ReadResource(assembly, "ParaTool.Core.Resources.Schema.CommonConditions.khn");
        ParseConditions(common, "Vanilla", schema);

        var amp = ReadResource(assembly, "ParaTool.Core.Resources.Schema.amp_conditions.khn");
        ParseConditions(amp, "AMP", schema);

        return schema;
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name);
        if (stream == null) return "";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void ParseModifiers(string text, StatsSchema schema)
    {
        string? currentType = null;
        List<FieldDef>? currentFields = null;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("modifier type "))
            {
                // Flush previous
                if (currentType != null && currentFields != null)
                    schema.Types[currentType] = new TypeDef { Name = currentType, Fields = currentFields };

                currentType = ExtractQuoted(trimmed, "modifier type ");
                currentFields = [];
            }
            else if (trimmed.StartsWith("modifier ") && currentFields != null)
            {
                // modifier "FieldName","ValueType"
                var parts = trimmed["modifier ".Length..].Split(',');
                if (parts.Length >= 2)
                {
                    var fieldName = parts[0].Trim().Trim('"');
                    var valueType = parts[1].Trim().Trim('"');
                    currentFields.Add(new FieldDef { Name = fieldName, ValueType = valueType });
                }
            }
        }

        // Flush last
        if (currentType != null && currentFields != null)
            schema.Types[currentType] = new TypeDef { Name = currentType, Fields = currentFields };
    }

    private static void ParseValueLists(string text, StatsSchema schema)
    {
        string? currentList = null;
        List<string>? currentValues = null;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            if (trimmed.StartsWith("valuelist "))
            {
                if (currentList != null && currentValues != null)
                    schema.ValueLists[currentList] = new ValueList { Name = currentList, Values = currentValues };

                currentList = ExtractQuoted(trimmed, "valuelist ");
                currentValues = [];
            }
            else if (trimmed.StartsWith("value ") && currentValues != null)
            {
                // value "Name" [optional description]
                var valueStr = trimmed["value ".Length..].Trim();
                var idx = valueStr.IndexOf('"');
                if (idx >= 0)
                {
                    var end = valueStr.IndexOf('"', idx + 1);
                    if (end > idx)
                        currentValues.Add(valueStr[(idx + 1)..end]);
                }
            }
        }

        if (currentList != null && currentValues != null)
            schema.ValueLists[currentList] = new ValueList { Name = currentList, Values = currentValues };
    }

    private static readonly Regex FunctionRegex = new(@"^function\s+(\w+)\s*\(([^)]*)\)", RegexOptions.Multiline);

    private static void ParseConditions(string text, string source, StatsSchema schema)
    {
        foreach (Match match in FunctionRegex.Matches(text))
        {
            var name = match.Groups[1].Value;
            var args = match.Groups[2].Value.Trim();
            var sig = string.IsNullOrEmpty(args) ? $"{name}()" : $"{name}({args})";

            // Avoid duplicates
            if (!schema.Conditions.Any(c => c.Name == name && c.Source == source))
                schema.Conditions.Add(new ConditionFunc { Name = name, Signature = sig, Source = source });
        }
    }

    private static string ExtractQuoted(string line, string prefix)
    {
        var rest = line[prefix.Length..].Trim();
        var start = rest.IndexOf('"');
        if (start < 0) return rest;
        var end = rest.IndexOf('"', start + 1);
        return end > start ? rest[(start + 1)..end] : rest[(start + 1)..];
    }
}
