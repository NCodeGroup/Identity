#region Copyright Preamble

// Copyright @ 2025 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Common interface for all persisted tenant resources.
/// </summary>
[PublicAPI]
public interface ISupportPersistedTenantResource : ISupportResource, ISupportTenantId, ISupportConcurrencyToken
{
    // nothing
}

/// <summary>
/// Provides the base implementation for a persisted tenant resource using the <see cref="ISupportPersistedTenantResource"/> abstraction.
/// </summary>
[PublicAPI]
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
    public required string ConcurrencyToken { get; set; }
}

/// <summary>
/// Provides the base implementation for a persisted tenant resource using the <see cref="ISupportPersistedTenantResource"/> abstraction.
/// </summary>
/// <typeparam name="TValue">The type of the persisted tenant resource.</typeparam>
[PublicAPI]
public abstract class PersistedTenantResource<TValue> : BasePersistedTenantResource
{
    /// <summary>
    /// Gets or sets the value of the resource.
    /// </summary>
    public required TValue Value { get; set; }
}
