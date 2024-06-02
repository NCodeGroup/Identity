#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

/// <summary>
/// Represents an entity framework data contract for an <c>OAuth</c> or <c>OpenID Connect</c> grant.
/// The complimentary DTO for this entity is <see cref="PersistedGrant"/>.
/// </summary>
[Index(nameof(TenantId), nameof(GrantType), nameof(HashedKey), IsUnique = true)]
[Index(nameof(TenantId), nameof(ClientId), IsUnique = false)]
[Index(nameof(TenantId), nameof(NormalizedSubjectId), IsUnique = false)]
[Index(nameof(TenantId), nameof(ExpiresWhen), IsUnique = false)]
public class GrantEntity : ISupportId, ISupportTenant, ISupportConcurrencyToken
{
    /// <inheritdoc />
    [Key]
    [UseIdGenerator]
    public required long Id { get; init; }

    /// <inheritdoc />
    [ForeignKey(nameof(Tenant))]
    public required long TenantId { get; init; }

    /// <summary>
    /// Gets or sets the type of grant.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.GrantType)]
    public required string GrantType { get; init; }

    /// <summary>
    /// Gets or sets the <c>SHA-256</c> hash of the natural key that uniquely identifies this entity.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.HashedKey)]
    public required string HashedKey { get; init; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the <c>ClientId</c> associated with this entity.
    /// </summary>
    [ForeignKey(nameof(Client))]
    public required long? ClientId { get; init; }

    /// <summary>
    /// Gets or sets the <c>SubjectId</c> associated with this entity.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.SubjectId)]
    public required string? SubjectId { get; init; }

    /// <summary>
    /// Gets or sets the value of <see cref="SubjectId"/> in uppercase so that lookups can be sargable for DBMS
    /// engines that don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(MaxLengths.SubjectId)]
    public required string? NormalizedSubjectId { get; init; }

    /// <summary>
    /// Gets or sets when this entity was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets or sets when this entity expires.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets or sets when this entity was consumed.
    /// </summary>
    public required DateTimeOffset? ConsumedWhen { get; set; }

    /// <summary>
    /// Gets or sets the serialized JSON for the payload.
    /// </summary>
    public required JsonElement Payload { get; init; }

    //

    /// <inheritdoc />
    public required TenantEntity Tenant { get; init; }

    /// <summary>
    /// Gets the navigation property for the associated client.
    /// </summary>
    public required ClientEntity? Client { get; init; }
}
