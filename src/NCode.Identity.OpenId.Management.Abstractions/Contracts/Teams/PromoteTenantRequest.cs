// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the request to preview or apply the promotion of one tenant's configuration into another tenant of the
/// same team.
/// </summary>
// See ADR-1425.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PromoteTenantRequest
{
    /// <summary>
    /// Gets the identifier of the tenant whose configuration is promoted.
    /// </summary>
    public required string SourceTenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant that receives the configuration. It differs from
    /// <see cref="SourceTenantId"/>.
    /// </summary>
    public required string TargetTenantId { get; init; }

    /// <summary>
    /// Gets the environment-bound setting values the target keeps for the resources the promotion writes. A resource
    /// the promotion writes holds the source's other settings, then the target's own environment-bound values, then
    /// the values given here.
    /// </summary>
    public List<TenantPromotionEnvironmentSettings> EnvironmentSettings { get; init; } = [];
}
