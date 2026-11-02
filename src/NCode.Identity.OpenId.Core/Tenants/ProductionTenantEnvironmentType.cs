// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the <c>production</c> tenant environment type: it selects the <c>production</c> security profile and does
/// not show the non-production banner.
/// </summary>
// See ADR-1289.
internal sealed class ProductionTenantEnvironmentType : ITenantEnvironmentType
{
    /// <inheritdoc />
    public string TypeName => OpenIdConstants.TenantEnvironmentTypes.Production;

    /// <inheritdoc />
    public bool IsProduction => true;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SecurityProfiles { get; } =
    [OpenIdConstants.SecurityProfiles.Production];
}
