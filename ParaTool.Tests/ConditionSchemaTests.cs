using Xunit;
using ParaTool.Core.Schema;

namespace ParaTool.Tests;

/// <summary>
/// Tests for the condition catalog built from the embedded .khn files. Acts as a canary
/// that the bundled amp_conditions.khn stays in sync with the AMP release (the file is a
/// schema source for the chip menu — out-of-date = missing conditions for users).
/// </summary>
public class ConditionSchemaTests
{
    [Fact]
    public void Schema_Loads_WithoutError()
    {
        var schema = ConditionSchema.Instance;
        Assert.NotEmpty(schema.Functions);
        // Core tag condition must be present.
        Assert.True(schema.ByName.ContainsKey("Tagged"));
    }

    [Theory]
    // Conditions introduced in the June AMP release (Ilmater Expansion + helpers).
    [InlineData("IsBladeWarded")]
    [InlineData("IsAMPIlmaterStigmaApplied")]
    [InlineData("HasNegativeStatus")]
    public void Schema_Includes_ReleaseAmpConditions(string name)
    {
        Assert.True(ConditionSchema.Instance.ByName.ContainsKey(name),
            $"Expected AMP release condition '{name}' — bundled amp_conditions.khn may be stale.");
    }
}
