// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Tenants.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyRolloverOptionsValidatorTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WithTheDefaults_Succeeds()
    {
        var result = new TenantKeyRolloverOptionsValidator().Validate(null, new OpenIdOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WhenTheIntervalIsNotATimerPeriod_FailsNamingTheSection(int seconds)
    {
        var options = new OpenIdOptions
        {
            Tenant = { KeyRollover = { Interval = TimeSpan.FromSeconds(seconds) } },
        };

        var result = new TenantKeyRolloverOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.StartsWith(
            "Tenant:KeyRollover:Interval must be between",
            failure,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenATokenLifetimeSettingIsBlank_FailsNamingTheSection()
    {
        var options = new OpenIdOptions
        {
            Tenant = { KeyRollover = { TokenLifetimeSettings = ["id_token_lifetime", " "] } },
        };

        var result = new TenantKeyRolloverOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.Equal(
            "Tenant:KeyRollover:TokenLifetimeSettings must name a setting in every entry.",
            failure
        );
    }

    #endregion
}
