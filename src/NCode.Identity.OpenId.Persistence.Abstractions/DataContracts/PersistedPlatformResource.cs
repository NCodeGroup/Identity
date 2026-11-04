// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Common interface for all persisted server resources.
/// </summary>
[PublicAPI]
public interface ISupportPersistedPlatformResource
    : ISupportResource,
        ISupportPlatformId,
        ISupportConcurrencyToken
{
    // nothing
}

/// <summary>
/// Provides the base implementation for a persisted server resource using the <see cref="ISupportPersistedPlatformResource"/> abstraction.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class BasePersistedPlatformResource : ISupportPersistedPlatformResource
{
    /// <summary>
    /// Gets the prefix for the resource type.
    /// </summary>
    [MaxLength(MaxLengths.ResourceType)]
    protected const string ResourceTypePrefix = OpenIdResourceTypes.Platform;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public abstract string ResourceType { get; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public virtual string ResourceId => PlatformId;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public required string PlatformId { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;
}

/// <summary>
/// Provides the base implementation for a persisted server resource using the <see cref="ISupportPersistedPlatformResource"/> abstraction.
/// </summary>
/// <typeparam name="TValue">The type of the persisted server resource.</typeparam>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class PersistedPlatformResource<TValue> : BasePersistedPlatformResource
{
    /// <summary>
    /// Gets or sets the value of the resource.
    /// </summary>
    public required TValue Value { get; set; }
}
