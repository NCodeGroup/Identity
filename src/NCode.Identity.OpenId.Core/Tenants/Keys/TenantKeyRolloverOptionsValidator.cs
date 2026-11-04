// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Time;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Validates the options of the tenant key rollover when the host starts: the pass interval is one a timer accepts,
/// and every token lifetime setting is named.
/// </summary>
// See ADR-1137 (configuration is validated at startup), ADR-1221 and ADR-1404.
internal sealed class TenantKeyRolloverOptionsValidator : IValidateOptions<OpenIdOptions>
{
    private static string Path(string member) =>
        $"{nameof(OpenIdOptions.Tenant)}:{nameof(OpenIdTenantOptions.KeyRollover)}:{member}";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rollover = options.Tenant.KeyRollover;
        List<string> failures = [];

        if (!TimerPeriod.IsValid(rollover.Interval))
        {
            failures.Add(
                $"{Path(nameof(OpenIdTenantKeyRolloverOptions.Interval))} must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {rollover.Interval}."
            );
        }

        if (rollover.TokenLifetimeSettings.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add(
                $"{Path(nameof(OpenIdTenantKeyRolloverOptions.TokenLifetimeSettings))} must name a setting in every entry."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
