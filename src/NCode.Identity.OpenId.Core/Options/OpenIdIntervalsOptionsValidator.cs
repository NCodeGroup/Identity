// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Time;

namespace NCode.Identity.OpenId;

/// <summary>
/// Validates the refresh intervals and lifetimes that the server and the tenants run timers on when the host starts,
/// so a value that a timer refuses stops the host instead of failing the first time a timer is created.
/// </summary>
// See ADR-1221.
internal sealed class OpenIdIntervalsOptionsValidator : IValidateOptions<OpenIdOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        Check(
            failures,
            nameof(OpenIdOptions.Platform),
            nameof(OpenIdPlatformOptions.SettingsPeriodicRefreshInterval),
            options.Platform.SettingsPeriodicRefreshInterval
        );
        Check(
            failures,
            nameof(OpenIdOptions.Tenant),
            nameof(OpenIdTenantOptions.SettingsPeriodicRefreshInterval),
            options.Tenant.SettingsPeriodicRefreshInterval
        );
        Check(
            failures,
            nameof(OpenIdOptions.Tenant),
            nameof(OpenIdTenantOptions.SecretsPeriodicRefreshInterval),
            options.Tenant.SecretsPeriodicRefreshInterval
        );
        Check(
            failures,
            nameof(OpenIdOptions.Tenant),
            nameof(OpenIdTenantOptions.TenantCacheLifetime),
            options.Tenant.TenantCacheLifetime
        );
        Check(
            failures,
            nameof(OpenIdOptions.Team),
            nameof(OpenIdTeamOptions.SettingsPeriodicRefreshInterval),
            options.Team.SettingsPeriodicRefreshInterval
        );
        Check(
            failures,
            nameof(OpenIdOptions.Team),
            nameof(OpenIdTeamOptions.TeamCacheLifetime),
            options.Team.TeamCacheLifetime
        );
        Check(
            failures,
            nameof(OpenIdOptions.Team),
            nameof(OpenIdTeamOptions.ReconciliationLeaseDuration),
            options.Team.ReconciliationLeaseDuration
        );

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(List<string> failures, string section, string member, TimeSpan value)
    {
        if (TimerPeriod.IsValid(value))
            return;

        var option = $"{section}.{member}";
        var key = $"{section}:{member}";
        failures.Add(
            $"{nameof(OpenIdOptions)}.{option} ('{key}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {value}."
        );
    }
}
