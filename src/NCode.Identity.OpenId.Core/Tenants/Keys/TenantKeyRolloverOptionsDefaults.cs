// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Fills <see cref="OpenIdTenantKeyRolloverOptions.TokenLifetimeSettings"/> with
/// <see cref="OpenIdTenantKeyRolloverOptions.DefaultTokenLifetimeSettings"/> once the options are bound, when
/// configuration names no setting: a bound collection starts empty, because the binder adds configured values to
/// whatever the initializer put there, so configuration that names any setting replaces the defaults rather than
/// adding to them.
/// </summary>
// See ADR-1404.
internal sealed class TenantKeyRolloverOptionsDefaults : IPostConfigureOptions<OpenIdOptions>
{
    /// <inheritdoc />
    public void PostConfigure(string? name, OpenIdOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var tokenLifetimeSettings = options.Tenant.KeyRollover.TokenLifetimeSettings;
        if (tokenLifetimeSettings.Count == 0)
        {
            tokenLifetimeSettings.AddRange(
                OpenIdTenantKeyRolloverOptions.DefaultTokenLifetimeSettings
            );
        }
    }
}
