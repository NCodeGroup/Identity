// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Common interface for all persisted team resources.
/// </summary>
// See ADR-1403
[PublicAPI]
public interface ISupportPersistedTeamResource
    : ISupportResource,
        ISupportTeamId,
        ISupportConcurrencyToken
{
    // nothing
}

/// <summary>
/// Provides the base implementation for a persisted team resource using the <see cref="ISupportPersistedTeamResource"/> abstraction.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class BasePersistedTeamResource : ISupportPersistedTeamResource
{
    /// <summary>
    /// Gets the prefix for the resource type.
    /// </summary>
    [MaxLength(MaxLengths.ResourceType)]
    protected const string ResourceTypePrefix = OpenIdResourceTypes.Team;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public abstract string ResourceType { get; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public virtual string ResourceId => TeamId;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TeamId { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;
}

/// <summary>
/// Provides the base implementation for a persisted team resource using the <see cref="ISupportPersistedTeamResource"/> abstraction.
/// </summary>
/// <typeparam name="TValue">The type of the persisted team resource.</typeparam>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class PersistedTeamResource<TValue> : BasePersistedTeamResource
{
    /// <summary>
    /// Gets or sets the value of the resource.
    /// </summary>
    public required TValue Value { get; set; }
}
