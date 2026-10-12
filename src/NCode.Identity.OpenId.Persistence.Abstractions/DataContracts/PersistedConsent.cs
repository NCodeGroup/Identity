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
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted end-user consent record: the durable decision that a subject has authorized a
/// client for a set of scopes. Unlike the ephemeral <see cref="PersistedGrant"/> records (authorization codes, refresh
/// tokens, the session), a consent record is long-lived, survives logout, and is upserted in place — it is the
/// server-side memory that lets a returning subject skip the consent screen for scopes already granted.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedConsent : ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets the identifier of the tenant that owns this consent record.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets or sets the natural identifier of the client the subject consented to.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets or sets the identifier of the subject (end-user) that granted the consent.
    /// </summary>
    [MaxLength(OpenIdMaxLengths.SubjectId)]
    public required string SubjectId { get; init; }

    /// <summary>
    /// Gets or sets the set of scopes the subject was asked to decide on (the "decided" universe). It is the superset
    /// of every scope the subject has been presented with across their consent interactions, and it determines when a
    /// fresh consent screen must be shown: a request whose scopes are all contained here has already been decided, so
    /// the subject is not re-prompted; a request introducing a previously-unseen scope requires a new decision. It is
    /// always a superset of <see cref="GrantedScopes"/>.
    /// </summary>
    public required IReadOnlyCollection<string> RequestedScopes { get; set; }

    /// <summary>
    /// Gets or sets the set of scopes the subject has granted (allowed) to the client. The scopes actually issued for a
    /// request are the intersection of the request's scopes and this set (OIDC down-scoping).
    /// </summary>
    public required IReadOnlyCollection<string> GrantedScopes { get; set; }

    /// <summary>
    /// Gets or sets when this consent record was first created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets or sets when this consent record was last updated (for example, when the granted scopes were widened).
    /// </summary>
    public required DateTimeOffset UpdatedWhen { get; set; }

    /// <inheritdoc />
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }
}
