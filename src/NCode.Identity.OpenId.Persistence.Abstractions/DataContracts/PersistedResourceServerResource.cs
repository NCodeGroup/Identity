// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Common interface for all persisted resource server resources.
/// </summary>
[PublicAPI]
public interface ISupportPersistedResourceServerResource
    : ISupportResource,
        ISupportResourceServerId,
        ISupportConcurrencyToken,
        ISupportTenantId
{
    // nothing
}

/// <summary>
/// Provides the base implementation for a persisted resource server resource using the
/// <see cref="ISupportPersistedResourceServerResource"/> abstraction.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class BasePersistedResourceServerResource : ISupportPersistedResourceServerResource
{
    /// <summary>
    /// Gets the prefix for the resource type.
    /// </summary>
    [MaxLength(MaxLengths.ResourceType)]
    protected const string ResourceTypePrefix = OpenIdResourceTypes.ResourceServer;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public abstract string ResourceType { get; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public virtual string ResourceId => ResourceServerId;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public required string ResourceServerId { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }
}

/// <summary>
/// Provides the base implementation for a persisted resource server resource using the
/// <see cref="ISupportPersistedResourceServerResource"/> abstraction.
/// </summary>
/// <typeparam name="TValue">The type of the persisted resource server resource.</typeparam>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class PersistedResourceServerResource<TValue> : BasePersistedResourceServerResource
{
    /// <summary>
    /// Gets or sets the value of the resource.
    /// </summary>
    public required TValue Value { get; set; }
}
