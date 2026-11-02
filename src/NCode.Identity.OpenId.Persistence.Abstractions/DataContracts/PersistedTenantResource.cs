// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Common interface for all persisted tenant resources.
/// </summary>
[PublicAPI]
public interface ISupportPersistedTenantResource
    : ISupportResource,
        ISupportTenantId,
        ISupportConcurrencyToken
{
    // nothing
}

/// <summary>
/// Provides the base implementation for a persisted tenant resource using the <see cref="ISupportPersistedTenantResource"/> abstraction.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class BasePersistedTenantResource : ISupportPersistedTenantResource
{
    /// <summary>
    /// Gets the prefix for the resource type.
    /// </summary>
    [MaxLength(MaxLengths.ResourceType)]
    protected const string ResourceTypePrefix = OpenIdResourceTypes.Tenant;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceType)]
    public abstract string ResourceType { get; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public virtual string ResourceId => TenantId;

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <inheritdoc/>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public string ConcurrencyToken { get; set; } = string.Empty;
}

/// <summary>
/// Provides the base implementation for a persisted tenant resource using the <see cref="ISupportPersistedTenantResource"/> abstraction.
/// </summary>
/// <typeparam name="TValue">The type of the persisted tenant resource.</typeparam>
[PublicAPI]
[ExcludeFromCodeCoverage]
public abstract class PersistedTenantResource<TValue> : BasePersistedTenantResource
{
    /// <summary>
    /// Gets or sets the value of the resource.
    /// </summary>
    public required TValue Value { get; set; }
}
