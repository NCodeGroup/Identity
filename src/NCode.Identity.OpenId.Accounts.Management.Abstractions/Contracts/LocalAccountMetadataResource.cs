// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Accounts.Management.Contracts;

/// <summary>
/// Represents the REST resource for a local account's server-owned metadata bags, which live on the account's owning
/// principal and project into issued tokens and the UserInfo response. The
/// <see cref="ConcurrencyToken"/> is the owning principal's token.
/// </summary>
// See ADR-1055.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class LocalAccountMetadataResource : ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the identifier of the tenant the account belongs to.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the local account (its self-issued subject).
    /// </summary>
    public required string LocalAccountId { get; init; }

    /// <summary>
    /// Gets the opaque identifier of the account's owning principal (the actor the metadata belongs to).
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the owner-updatable profile metadata as a free-form JSON object.
    /// </summary>
    public required JsonElement ProfileMetadata { get; init; }

    /// <summary>
    /// Gets the server-controlled system metadata as a free-form JSON object. It is never end-user writable and may
    /// carry authorization-relevant data.
    /// </summary>
    public required JsonElement SystemMetadata { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }
}
