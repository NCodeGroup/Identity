// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;

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
    /// Determines whether a persisted secret satisfies a provisioned key: the server could select it for the key's
    /// algorithm and use, and it holds the private material that use requires.
    /// </summary>
    /// <param name="specification">The provisioned key.</param>
    /// <param name="secret">The persisted secret the tenant holds.</param>
    /// <returns><c>true</c> when the secret satisfies the provisioned key; otherwise <c>false</c>.</returns>
    bool IsSatisfiedBy(TenantKeySpecification specification, PersistedSecret secret);
}
