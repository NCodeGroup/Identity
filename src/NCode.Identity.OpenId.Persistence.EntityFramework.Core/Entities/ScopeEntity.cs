#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for a scope (a permission, in Auth0 terms) owned by a
/// <see cref="ResourceServerEntity"/>. A scope's identity is its <see cref="Value"/> within its resource server, so
/// the same value on two resource servers is two distinct scopes.
/// </summary>
[Index(nameof(ResourceServerId), nameof(NormalizedValue), IsUnique = true)]
public sealed class ScopeEntity : ISupportTenantEntity, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the surrogate identifier for this entity.
    /// </summary>
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <inheritdoc />
    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    /// <summary>
    /// Gets the owning tenant's normalized identifier, denormalized onto this entity so the tenant-scoping global
    /// query filter compares a single indexed column without a navigation join.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.ResourceId)]
    public required string NormalizedTenantId { get; init; }

    /// <summary>
    /// Gets the foreign key for the resource server that owns this scope.
    /// </summary>
    [ForeignKey(nameof(ResourceServer))]
    public required long ResourceServerId { get; init; }

    /// <summary>
    /// Gets the value of this scope (for example, <c>read:messages</c>).
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.ScopeValue)]
    public required string Value { get; init; }

    /// <summary>
    /// Gets the value of <see cref="Value"/> in uppercase so that lookups can be sargable for DBMS engines that don't
    /// support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.ScopeValue)]
    public required string NormalizedValue { get; init; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the human-readable description for this scope.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.Description)]
    public required string? Description { get; set; }

    /// <summary>
    /// Gets a value indicating whether this scope is a reserved, system-owned scope (such as a standard OpenID Connect
    /// scope) that cannot be deleted.
    /// </summary>
    public required bool IsSystem { get; init; }

    // navigational properties

    /// <inheritdoc />
    public required TenantEntity Tenant { get; init; }

    /// <summary>
    /// Gets the navigation property for the resource server that owns this scope.
    /// </summary>
    public required ResourceServerEntity ResourceServer { get; init; }
}
