using SecurityBaselineGUI.Core.Services;
using Xunit;

namespace SecurityBaselineGUI.Tests;

public sealed class AsrRuleCatalogTests
{
    [Fact]
    public void Rules_ContainsSixteenUniqueGuids()
    {
        Assert.Equal(16, AsrRuleCatalog.Rules.Count);
        Assert.Equal(AsrRuleCatalog.Rules.Count, AsrRuleCatalog.Rules.Select(r => r.Guid).Distinct().Count());
    }
}
