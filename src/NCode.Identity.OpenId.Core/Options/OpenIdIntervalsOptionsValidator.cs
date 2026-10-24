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

using Microsoft.Extensions.Options;
using NCode.Time;

namespace NCode.Identity.OpenId;

/// <summary>
/// Validates the refresh intervals and lifetimes that the server and the tenants run timers on when the host starts,
/// so a value that a timer refuses stops the host instead of failing the first time a timer is created.
/// </summary>
// See ADR-0221.
internal sealed class OpenIdIntervalsOptionsValidator : IValidateOptions<OpenIdOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        Check(
            failures,
            nameof(OpenIdOptions.Server),
            nameof(OpenIdServerOptions.SettingsPeriodicRefreshInterval),
            options.Server.SettingsPeriodicRefreshInterval
        );
        Check(
            failures,
            nameof(OpenIdOptions.Server),
            nameof(OpenIdServerOptions.SecretsPeriodicRefreshInterval),
            options.Server.SecretsPeriodicRefreshInterval
        );
        Check(
            failures,
            nameof(OpenIdOptions.Server),
            nameof(OpenIdServerOptions.ReconciliationLeaseDuration),
            options.Server.ReconciliationLeaseDuration
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
