// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Contains the options used to configure how the ambient tenant is resolved from an HTTP request. This is the
/// shared, low-level tenant-selection configuration consumed by both the OpenID runtime and the management API.
/// </summary>
[PublicAPI]
public sealed class TenantResolutionOptions
{
    /// <summary>
    /// Contains the placeholder an issuer template carries where the tenant's identifier goes — the single variable of
    /// an RFC 6570 level-1 template. A tenant identifier is a lowercase host label, so it is substituted as-is.
    /// </summary>
    public const string TenantIdPlaceholder = "{tenantId}";

    /// <summary>
    /// Gets or sets the strategy code that selects the tenant-resolution strategy.
    /// This value is used to find the corresponding <see cref="ITenantStrategy"/>.
    /// The default value is <see cref="OpenIdConstants.TenantStrategyCodes.StaticSingle"/>.
    /// </summary>
    public string StrategyCode { get; set; } = OpenIdConstants.TenantStrategyCodes.StaticSingle;

    /// <summary>
    /// Gets or sets the options for the root (control-plane) tenant, into which the control-plane management resource
    /// server is seeded.
    /// </summary>
    public RootTenantOptions Root { get; set; } = new();

    /// <summary>
    /// Gets or sets the options used when <see cref="StrategyCode"/> is
    /// <see cref="OpenIdConstants.TenantStrategyCodes.StaticSingle"/>.
    /// </summary>
    public StaticSingleTenantOptions? StaticSingle { get; set; }

    /// <summary>
    /// Gets or sets the options used when <see cref="StrategyCode"/> is
    /// <see cref="OpenIdConstants.TenantStrategyCodes.DynamicByHost"/>.
    /// </summary>
    public DynamicByHostTenantOptions? DynamicByHost { get; set; }

    /// <summary>
    /// Gets or sets the options used when <see cref="StrategyCode"/> is
    /// <see cref="OpenIdConstants.TenantStrategyCodes.DynamicByPath"/>.
    /// </summary>
    public DynamicByPathTenantOptions? DynamicByPath { get; set; }
}
