// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents the REST resource for the report of a promotion: what it changes, what it notes, and what refuses it.
/// </summary>
// See ADR-1425.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantPromotionResource
{
    /// <summary>
    /// Gets the identifier of the team both tenants belong to.
    /// </summary>
    public required string TeamId { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant whose configuration is promoted.
    /// </summary>
    public required string SourceTenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant that receives the configuration.
    /// </summary>
    public required string TargetTenantId { get; init; }

    /// <summary>
    /// Gets a value indicating whether the changes were written. A preview is never applied.
    /// </summary>
    public required bool IsApplied { get; init; }

    /// <summary>
    /// Gets the change the promotion makes, or made, to each resource it covers.
    /// </summary>
    public required List<TenantPromotionChangeResource> Changes { get; init; }

    /// <summary>
    /// Gets the notes about what the promotion left out or what the target still needs.
    /// </summary>
    public required List<TenantPromotionNoteResource> Notes { get; init; }

    /// <summary>
    /// Gets the errors that refuse the promotion. A promotion with an error writes nothing.
    /// </summary>
    public required List<TenantPromotionErrorResource> Errors { get; init; }
}
