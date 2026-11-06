// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Teams;

/// <summary>
/// Represents an error that refuses a promotion.
/// </summary>
// See ADR-1425.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class TenantPromotionErrorResource
{
    /// <summary>
    /// Gets the type of the resource the error is about.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets the human-readable detail of the error.
    /// </summary>
    public required string Detail { get; init; }

    /// <summary>
    /// Gets the identifier of the resource the error is about, when it is about one.
    /// </summary>
    public string? ResourceId { get; init; }
}
