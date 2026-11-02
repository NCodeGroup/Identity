// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the <c>testing</c> tenant environment type: a non-production environment that selects no additional
/// security profiles.
/// </summary>
// See ADR-1289.
internal sealed class TestingTenantEnvironmentType : ITenantEnvironmentType
{
    /// <inheritdoc />
    public string TypeName => OpenIdConstants.TenantEnvironmentTypes.Testing;

    /// <inheritdoc />
    public bool IsProduction => false;

    /// <inheritdoc />
    public IReadOnlyCollection<string> SecurityProfiles { get; } = [];
}
