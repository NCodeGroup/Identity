// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options that are used to configure how a resolved tenant is materialized (settings/secrets refresh
/// and caching), which keys every tenant is provisioned with, and how they roll over. Tenant <em>selection</em> is configured separately via
/// <see cref="NCode.Identity.OpenId.Tenants.TenantResolutionOptions"/>.
/// </summary>
[PublicAPI]
public sealed class OpenIdTenantOptions
{
    /// <summary>
    /// Gets or sets the period of time after which the settings for a tenant are refreshed.
    /// The default value is 5 minutes.
    /// </summary>
    public TimeSpan SettingsPeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(5.0);

    /// <summary>
    /// Gets or sets the period of time after which the secrets for a tenant are refreshed.
    /// The default value is 15 minutes.
    /// </summary>
    public TimeSpan SecretsPeriodicRefreshInterval { get; set; } = TimeSpan.FromMinutes(15.0);

    /// <summary>
    /// Gets or sets the period of time after which a materialized tenant is removed from the cache, however often
    /// it is used, so a replica that missed the announcement of a change to the tenant serves it at most this long.
    /// The default value is 15 minutes.
    /// </summary>
    public TimeSpan TenantCacheLifetime { get; set; } = TimeSpan.FromMinutes(15.0);

    /// <summary>
    /// Gets or sets the options that decide which keys every tenant is provisioned with when it is seeded.
    /// </summary>
    public OpenIdTenantKeyProvisioningOptions KeyProvisioning { get; set; } = new();

    /// <summary>
    /// Gets or sets the options of the rollover of the keys every tenant is provisioned with.
    /// </summary>
    public OpenIdTenantKeyRolloverOptions KeyRollover { get; set; } = new();
}
