// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyUsageTests : BaseTests
{
    private const string FirstSetting = "first_alg_values_supported";
    private const string SecondSetting = "second_alg_values_supported";
    private const string UnnamedSetting = "unnamed_alg_values_supported";

    #region IsUsed Tests

    [Fact]
    public void IsUsed_WhenANamedSettingListsTheAlgorithm_ReturnsTrue()
    {
        var settings = CreateSettings((SecondSetting, ["RS256"]));

        var result = TenantKeyUsage.IsUsed([FirstSetting, SecondSetting], settings, "RS256");

        Assert.True(result);
    }

    [Fact]
    public void IsUsed_WhenOnlyAnUnnamedSettingListsTheAlgorithm_ReturnsFalse()
    {
        var settings = CreateSettings((FirstSetting, ["ES256"]), (UnnamedSetting, ["RS256"]));

        var result = TenantKeyUsage.IsUsed([FirstSetting, SecondSetting], settings, "RS256");

        Assert.False(result);
    }

    #endregion

    private IReadOnlySettingCollection CreateSettings(
        params (string SettingName, string[] Algorithms)[] lists
    )
    {
        // Each setting lists its algorithms; every other lookup finds nothing.
        var mockSettings = CreateLooseMock<ISettingCollection>();
        foreach (var (settingName, algorithms) in lists)
        {
            object? value = algorithms.ToList();
            mockSettings.Setup(x => x.TryGetValue(settingName, out value)).Returns(true);
        }

        return mockSettings.Object;
    }
}
