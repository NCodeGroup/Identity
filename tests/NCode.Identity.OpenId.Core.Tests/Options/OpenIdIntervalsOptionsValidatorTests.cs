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
    public void Validate_WhenTheLeaseDurationIsZero_FailsNamingItsKey()
    {
        var options = new OpenIdOptions
        {
            Server = new OpenIdServerOptions { ReconciliationLeaseDuration = TimeSpan.Zero },
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'Server:ReconciliationLeaseDuration'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
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

    [Fact]
    public void Validate_WhenTwoIntervalsAreNegative_ReportsBoth()
    {
        var options = new OpenIdOptions
        {
            Server = new OpenIdServerOptions
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
