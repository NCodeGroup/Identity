// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the <c>staging</c> tenant environment type: it mirrors production's rules by selecting the
/// <c>production</c> security profile, while still showing the non-production banner.
/// </summary>
// See ADR-1289.
internal sealed class StagingTenantEnvironmentType : ITenantEnvironmentType
{
    /// <inheritdoc />
    public string TypeName => OpenIdConstants.TenantEnvironmentTypes.Staging;

    /// <inheritdoc />
    public bool IsProduction => false;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SecurityProfiles { get; } =
    [OpenIdConstants.SecurityProfiles.Production];
}
