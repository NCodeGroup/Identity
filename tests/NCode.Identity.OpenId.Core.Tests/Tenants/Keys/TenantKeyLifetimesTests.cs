// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyLifetimesTests : BaseTests
{
    private Mock<IReadOnlySettingCollection> MockSettings { get; }

    public TenantKeyLifetimesTests()
    {
        // A value-provider stub, loose so a token lifetime name the test leaves unset reads as absent; every read
        // reaches the four lifecycle settings, so each setup is verified.
        MockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TenantKeyLifetime))
            .Returns(TimeSpan.FromDays(90))
            .Verifiable();
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TenantKeyActivationDelay))
            .Returns(TimeSpan.FromDays(2))
            .Verifiable();
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TenantKeyRetirementGrace))
            .Returns(TimeSpan.FromDays(7))
            .Verifiable();
        MockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ClockSkew))
            .Returns(TimeSpan.FromMinutes(5))
            .Verifiable();
    }

    private void SetupTokenLifetime(string settingName, TimeSpan lifetime)
    {
        object? value = lifetime;
        MockSettings.Setup(x => x.TryGetValue(settingName, out value)).Returns(true).Verifiable();
    }

    #region Read Tests

    [Fact]
    public void Read_ReadsTheLifetimesFromTheSettings()
    {
        var lifetimes = TenantKeyLifetimes.Read(MockSettings.Object, []);

        Assert.Equal(TimeSpan.FromDays(90), lifetimes.Lifetime);
        Assert.Equal(TimeSpan.FromDays(2), lifetimes.ActivationDelay);
        Assert.Equal(TimeSpan.FromDays(7), lifetimes.RetirementGrace);
    }

    [Fact]
    public void Read_WhenATokenOutlivesTheGrace_RaisesTheGraceToItsLifetimePlusTheClockSkew()
    {
        SetupTokenLifetime("short_token_lifetime", TimeSpan.FromDays(1));
        SetupTokenLifetime("long_token_lifetime", TimeSpan.FromDays(30));

        var lifetimes = TenantKeyLifetimes.Read(
            MockSettings.Object,
            ["short_token_lifetime", "long_token_lifetime", "absent_lifetime"]
        );

        Assert.Equal(TimeSpan.FromDays(30) + TimeSpan.FromMinutes(5), lifetimes.RetirementGrace);
    }

    [Fact]
    public void Read_WhenEveryTokenEndsWithinTheGrace_KeepsTheConfiguredGrace()
    {
        SetupTokenLifetime("short_token_lifetime", TimeSpan.FromHours(1));

        var lifetimes = TenantKeyLifetimes.Read(MockSettings.Object, ["short_token_lifetime"]);

        Assert.Equal(TimeSpan.FromDays(7), lifetimes.RetirementGrace);
    }

    [Fact]
    public void Read_WhenATokenLifetimeSettingIsNotADuration_IgnoresIt()
    {
        object? value = "not a duration";
        MockSettings.Setup(x => x.TryGetValue("odd_setting", out value)).Returns(true).Verifiable();

        var lifetimes = TenantKeyLifetimes.Read(MockSettings.Object, ["odd_setting"]);

        Assert.Equal(TimeSpan.FromDays(7), lifetimes.RetirementGrace);
    }

    #endregion
}
