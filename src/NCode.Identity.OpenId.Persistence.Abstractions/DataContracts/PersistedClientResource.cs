// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Common interface for all persisted client resources.
/// </summary>
[PublicAPI]
public interface ISupportPersistedClientResource
    : ISupportResource,
        ISupportClientId,
        ISupportConcurrencyToken,
        ISupportTenantId
{
    // nothing
}

/// <summary>
/// Provides the base implementation for a persisted client resource using the <see cref="ISupportPersistedClientResource"/> abstraction.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class BasePersistedClientResource : ISupportPersistedClientResource
{
    /// <summary>
    /// Gets the prefix for the resource type.
    /// </summary>
    [MaxLength(MaxLengths.ResourceType)]
    protected const string ResourceTypePrefix = OpenIdResourceTypes.Client;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public abstract string ResourceType { get; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public virtual string ResourceId => ClientId;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public required string ClientId { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }
}

/// <summary>
/// Provides the base implementation for a persisted client resource using the <see cref="ISupportPersistedClientResource"/> abstraction.
/// </summary>
/// <typeparam name="TValue">The type of the persisted client resource.</typeparam>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class PersistedClientResource<TValue> : BasePersistedClientResource
{
    /// <summary>
    /// Gets or sets the value of the resource.
    /// </summary>
    public required TValue Value { get; set; }
}
