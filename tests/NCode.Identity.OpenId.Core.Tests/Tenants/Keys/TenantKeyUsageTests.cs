#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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

    #region FindAdded Tests

    [Fact]
    public void FindAdded_WhenANamedListGainsAnAlgorithm_ReturnsIt()
    {
        var previous = CreateSettings((FirstSetting, ["RS256"]));
        var current = CreateSettings((FirstSetting, ["RS256", "ES256"]));

        var result = TenantKeyUsage.FindAdded(
            [FirstSetting, SecondSetting],
            previous,
            current,
            ["RS256", "ES256", "ES256", "PS256"]
        );

        Assert.Equal(["ES256"], result);
    }

    [Fact]
    public void FindAdded_WhenAnotherListAlreadyNamedTheAlgorithm_StillReturnsIt()
    {
        // The first list named the algorithm before and after; the second list is the one that gained it.
        var previous = CreateSettings((FirstSetting, ["ES256"]), (SecondSetting, ["RS256"]));
        var current = CreateSettings(
            (FirstSetting, ["ES256"]),
            (SecondSetting, ["RS256", "ES256"])
        );

        var result = TenantKeyUsage.FindAdded(
            [FirstSetting, SecondSetting],
            previous,
            current,
            ["ES256"]
        );

        Assert.Equal(["ES256"], result);
    }

    [Fact]
    public void FindAdded_WhenNoListGainsAnAlgorithm_ReturnsEmpty()
    {
        // A list that loses an algorithm, and an unnamed list that gains one, add nothing.
        var previous = CreateSettings((FirstSetting, ["RS256", "ES256"]));
        var current = CreateSettings((FirstSetting, ["RS256"]), (UnnamedSetting, ["ES256"]));

        var result = TenantKeyUsage.FindAdded(
            [FirstSetting, SecondSetting],
            previous,
            current,
            ["RS256", "ES256"]
        );

        Assert.Empty(result);
    }

    [Fact]
    public void FindAdded_WhenAListThatWasSetIsUnset_ReturnsTheAlgorithmsItDidNotName()
    {
        // An unset list is unrestricted, so lifting the list permits every algorithm it did not name.
        var previous = CreateSettings((FirstSetting, ["RS256"]));
        var current = CreateSettings();

        var result = TenantKeyUsage.FindAdded(
            [FirstSetting, SecondSetting],
            previous,
            current,
            ["RS256", "PS256"]
        );

        Assert.Equal(["PS256"], result);
    }

    [Fact]
    public void FindAdded_WhenAListWasNeverSet_ReturnsEmpty()
    {
        var previous = CreateSettings();
        var current = CreateSettings();

        var result = TenantKeyUsage.FindAdded(
            [FirstSetting, SecondSetting],
            previous,
            current,
            ["RS256", "PS256"]
        );

        Assert.Empty(result);
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
