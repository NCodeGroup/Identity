// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Time;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests;

public sealed class OpenIdIntervalsOptionsValidatorTests
{
    private OpenIdIntervalsOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenEveryIntervalIsTheDefault_Succeeds()
    {
        var options = new OpenIdOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheTenantCacheLifetimeIsLongerThanATimerAccepts_FailsNamingItsKey()
    {
        var options = new OpenIdOptions
        {
            Tenant = new OpenIdTenantOptions
            {
                TenantCacheLifetime = TimerPeriod.MaxValue + TimeSpan.FromMilliseconds(1),
            },
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'Tenant:TenantCacheLifetime'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData(nameof(OpenIdTeamOptions.SettingsPeriodicRefreshInterval))]
    [InlineData(nameof(OpenIdTeamOptions.TeamCacheLifetime))]
    [InlineData(nameof(OpenIdTeamOptions.ReconciliationLeaseDuration))]
    public void Validate_WhenATeamIntervalIsNegative_FailsNamingItsKey(string member)
    {
        var team = new OpenIdTeamOptions();
        typeof(OpenIdTeamOptions).GetProperty(member)!.SetValue(team, TimeSpan.FromMinutes(-1));
        var options = new OpenIdOptions { Team = team };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains($"'Team:{member}'", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenTwoIntervalsAreNegative_ReportsBoth()
    {
        var options = new OpenIdOptions
        {
            Platform = new OpenIdPlatformOptions
            {
                SettingsPeriodicRefreshInterval = TimeSpan.FromMinutes(-1),
            },
            Tenant = new OpenIdTenantOptions
            {
                SecretsPeriodicRefreshInterval = TimeSpan.FromMinutes(-1),
            },
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.NotNull(result.Failures);
        Assert.Equal(2, result.Failures.Count());
    }

    #endregion
}
