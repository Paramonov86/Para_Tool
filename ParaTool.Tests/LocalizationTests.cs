using Xunit;
using ParaTool.Core.Localization;

namespace ParaTool.Tests;

public class HandleGeneratorTests
{
    [Fact]
    public void New_StartsWithH_NoDashes()
    {
        var h = HandleGenerator.New();
        Assert.StartsWith("h", h);
        Assert.DoesNotContain("-", h);
        Assert.Contains("g", h);
    }

    [Fact]
    public void New_IsUnique()
    {
        var a = HandleGenerator.New();
        var b = HandleGenerator.New();
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void New_PassesValidation()
    {
        Assert.True(HandleGenerator.IsValid(HandleGenerator.New()));
    }

    [Fact]
    public void NewPair_ReturnsTwoDistinctHandles()
    {
        var (a, b) = HandleGenerator.NewPair();
        Assert.NotEqual(a, b);
        Assert.True(HandleGenerator.IsValid(a));
        Assert.True(HandleGenerator.IsValid(b));
    }

    [Fact]
    public void FormatWithVersion_AppendsSemicolonAndNumber()
    {
        Assert.Equal("h123;1", HandleGenerator.FormatWithVersion("h123", 1));
        Assert.Equal("h123;5", HandleGenerator.FormatWithVersion("h123", 5));
    }

    [Fact]
    public void FormatWithVersion_DefaultsToOne()
    {
        Assert.Equal("h123;1", HandleGenerator.FormatWithVersion("h123"));
    }

    [Fact]
    public void Parse_SplitsHandleAndVersion()
    {
        var (h, v) = HandleGenerator.Parse("h5bb2726cg6840g4bc8;3");
        Assert.Equal("h5bb2726cg6840g4bc8", h);
        Assert.Equal(3, v);
    }

    [Fact]
    public void Parse_MissingVersion_DefaultsToOne()
    {
        var (h, v) = HandleGenerator.Parse("h5bb2726cg6840g4bc8");
        Assert.Equal("h5bb2726cg6840g4bc8", h);
        Assert.Equal(1, v);
    }

    [Fact]
    public void Parse_InvalidVersion_DefaultsToOne()
    {
        var (h, v) = HandleGenerator.Parse("h5bb;bad");
        Assert.Equal("h5bb", h);
        Assert.Equal(1, v);
    }

    [Fact]
    public void IsValid_HappyPath()
    {
        Assert.True(HandleGenerator.IsValid("h5bb2726cg6840g4bc8g82c0g30bf483ee1b7"));
    }

    [Fact]
    public void IsValid_MissingHPrefix()
    {
        Assert.False(HandleGenerator.IsValid("5bb2726cg6840g4bc8g82c0g30bf483ee1b7"));
    }

    [Fact]
    public void IsValid_TooShort()
    {
        Assert.False(HandleGenerator.IsValid("habc"));
    }

    [Fact]
    public void Parse_FormatWithVersion_Roundtrip()
    {
        var orig = HandleGenerator.New();
        var formatted = HandleGenerator.FormatWithVersion(orig, 7);
        var (h, v) = HandleGenerator.Parse(formatted);
        Assert.Equal(orig, h);
        Assert.Equal(7, v);
    }
}

public class TransliteratorTests
{
    [Fact]
    public void LatinPassesThrough()
    {
        Assert.Equal("Hello World", Transliterator.ToLatin("Hello World"));
    }

    [Fact]
    public void CyrillicLower_Transliterated()
    {
        Assert.Equal("privet", Transliterator.ToLatin("привет"));
    }

    [Fact]
    public void CyrillicUpper_Transliterated()
    {
        Assert.Equal("PRIVET", Transliterator.ToLatin("ПРИВЕТ"));
    }

    [Fact]
    public void MixedCase_HandlesBoth()
    {
        Assert.Equal("Moskva", Transliterator.ToLatin("Москва"));
    }

    [Fact]
    public void MultiCharMappings_ExpandCorrectly()
    {
        // ё→yo, ж→zh, щ→shch
        Assert.Equal("yozh", Transliterator.ToLatin("ёж"));
        Assert.Equal("borshch", Transliterator.ToLatin("борщ"));
    }

    [Fact]
    public void SoftHardSigns_Dropped()
    {
        // ъ and ь produce empty string (char-by-char, no contextual expansion)
        Assert.Equal("obem", Transliterator.ToLatin("объем"));
        Assert.Equal("konka", Transliterator.ToLatin("конька"));
    }

    [Fact]
    public void UkrainianExtras_Mapped()
    {
        Assert.Equal("Yizhak", Transliterator.ToLatin("Їжак"));
        Assert.Equal("Yevropa", Transliterator.ToLatin("Європа"));
    }

    [Fact]
    public void SpacesPreserved()
    {
        Assert.Equal("test moy", Transliterator.ToLatin("test мой"));
    }

    [Fact]
    public void PunctuationPassesThrough()
    {
        Assert.Equal("test!-?", Transliterator.ToLatin("test!-?"));
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        Assert.Equal("", Transliterator.ToLatin(""));
    }

    [Fact]
    public void NullInput_ReturnsNull()
    {
        Assert.Null(Transliterator.ToLatin(null!));
    }

    [Fact]
    public void RealItemName_YieldsValidStatId()
    {
        // This is the exact use case from CreateNewArtifact
        var name = "Тестовая шмотка";
        var result = Transliterator.ToLatin(name);
        // Should be fully Latin characters + space
        Assert.DoesNotMatch("[А-яЁё]", result);
    }
}
