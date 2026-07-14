using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class ScriptParameterServiceTests
{
    private static string ScriptPath => Path.Combine(AppContext.BaseDirectory, "TestData", "Configure-SecurityBaseline.ps1");

    [Fact]
    public void GetParameters_DetectsTargetOUAsMandatoryString()
    {
        var service = new ScriptParameterService();

        var parameters = service.GetParameters(ScriptPath);

        var targetOU = Assert.Single(parameters, p => p.Name == "TargetOU");
        Assert.True(targetOU.IsMandatory);
        Assert.Equal("String", targetOU.TypeName);
    }

    [Fact]
    public void GetParameters_DetectsAsrActionValidateSet()
    {
        var service = new ScriptParameterService();

        var parameters = service.GetParameters(ScriptPath);

        var asrAction = Assert.Single(parameters, p => p.Name == "AsrAction");
        Assert.NotNull(asrAction.ValidateSetValues);
        Assert.Contains("Audit", asrAction.ValidateSetValues!);
        Assert.Contains("Block", asrAction.ValidateSetValues!);
    }

    [Fact]
    public void GetParameters_DetectsLapsPasswordLengthRange()
    {
        var service = new ScriptParameterService();

        var parameters = service.GetParameters(ScriptPath);

        var length = Assert.Single(parameters, p => p.Name == "LapsPasswordLength");
        Assert.Equal(14, length.ValidateRangeMin);
        Assert.Equal(64, length.ValidateRangeMax);
    }

    [Fact]
    public void GetParameters_DetectsSwitchParameters()
    {
        var service = new ScriptParameterService();

        var parameters = service.GetParameters(ScriptPath);

        Assert.True(parameters.Single(p => p.Name == "SkipSchemaExtension").IsSwitch);
        Assert.True(parameters.Single(p => p.Name == "LsaProtectionUefiLock").IsSwitch);
    }

    [Fact]
    public void GetParameters_DetectsAsrRuleModesAndExclusionPaths()
    {
        var service = new ScriptParameterService();

        var parameters = service.GetParameters(ScriptPath);

        Assert.Contains(parameters, p => p.Name == "AsrRuleModes" && p.TypeName == "Hashtable");
        Assert.Contains(parameters, p => p.Name == "ExclusionPaths" && p.TypeName == "String[]");
    }
}
