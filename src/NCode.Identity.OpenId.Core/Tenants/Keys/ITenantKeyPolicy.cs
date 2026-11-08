// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Decides which keys every tenant is provisioned with and whether a key a tenant already holds satisfies one of them.
/// The startup validation of the provisioning options and the seed handler that provisions the keys apply this one
/// rule set, so a configuration the host accepts is exactly one the seed can honor.
/// </summary>
internal interface ITenantKeyPolicy
{
    /// <summary>
    /// Resolves the keys every tenant is provisioned with from the configured provisioning options, applying the
    /// default key set when none is configured.
    /// </summary>
    /// <param name="options">The configured <see cref="OpenIdTenantKeyProvisioningOptions"/>.</param>
    /// <returns>The resolved keys, and a reason for every configured key that cannot be provisioned.</returns>
    TenantKeySpecificationResolution ResolveSpecifications(
        OpenIdTenantKeyProvisioningOptions options
    );

    /// <summary>
    /// Applies a tenant's custody choice to the provisioned keys: when the tenant's
    /// <c>tenant_signing_key_provider</c> setting names a remote key provider, every signature key is held by that
    /// provider; encryption keys, and every key when the setting is empty, are held in the server's store.
    /// </summary>
    /// <param name="specifications">The provisioned keys resolved from the provisioning options.</param>
    /// <param name="settings">The tenant's effective settings.</param>
    /// <returns>The provisioned keys with the tenant's custody.</returns>
    IReadOnlyList<TenantKeySpecification> ForTenant(
        IReadOnlyList<TenantKeySpecification> specifications,
        IReadOnlySettingCollection settings
    );

    /// <summary>
    /// Determines whether a persisted secret satisfies a provisioned key: the server could select it for the key's
    /// algorithm and use, it holds the private material that use requires, and it is held by the key's custody (a
    /// remote key of the specification's provider, or a key that is not remote).
    /// </summary>
    /// <param name="specification">The provisioned key.</param>
    /// <param name="secret">The persisted secret the tenant holds.</param>
    /// <returns><c>true</c> when the secret satisfies the provisioned key; otherwise <c>false</c>.</returns>
    bool IsSatisfiedBy(TenantKeySpecification specification, PersistedSecret secret);
}
