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
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

/// <summary>
/// Represents an entity framework data contract for a durable end-user consent record: the decision that a subject has
/// authorized a client for a set of scopes. Unlike <see cref="GrantEntity"/>, a consent record is long-lived, survives
/// session termination, and is upserted by its natural (tenant, client, subject) key. The complimentary DTO for this
/// entity is <c>PersistedConsent</c>.
/// </summary>
[Index(nameof(TenantId), nameof(ClientId), nameof(NormalizedSubjectId), IsUnique = true)]
[Index(nameof(TenantId), nameof(NormalizedSubjectId))]
public sealed class ConsentEntity : ISupportTenantEntity, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
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
    /// Gets the <c>ClientId</c> (surrogate key) of the client the subject consented to.
    /// </summary>
    [ForeignKey(nameof(Client))]
    public required long ClientId { get; init; }

    /// <summary>
    /// Gets the identifier of the subject (end-user) that granted the consent.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public required string SubjectId { get; init; }

    /// <summary>
    /// Gets the value of <see cref="SubjectId"/> in uppercase so that lookups can be sargable for DBMS engines that
    /// don't support case-insensitive indices.
    /// </summary>
    [Unicode(false)]
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public required string NormalizedSubjectId { get; init; }

    /// <summary>
    /// Gets or sets the JSON-serialized array of scopes the subject was asked to decide on (the "decided" universe,
    /// a superset of <see cref="GrantedScopesJson"/>). A request whose scopes are all contained here has already been
    /// decided and does not re-prompt the subject.
    /// </summary>
    public required JsonElement RequestedScopesJson { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized array of scopes the subject has granted (allowed) to the client.
    /// </summary>
    public required JsonElement GrantedScopesJson { get; set; }

    /// <summary>
    /// Gets when this consent record was first created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets or sets when this consent record was last updated.
    /// </summary>
    public required DateTimeOffset UpdatedWhen { get; set; }

    //

    /// <inheritdoc />
    [Unicode(false)]
    [MaxLength(MaxLengths.ConcurrencyToken)]
    [ConcurrencyCheck]
    public required string ConcurrencyToken { get; set; }

    // navigational properties

    /// <inheritdoc />
    public required TenantEntity Tenant { get; init; }

    /// <summary>
    /// Gets the navigation property for the associated client.
    /// </summary>
    public required ClientEntity Client { get; init; }
}
