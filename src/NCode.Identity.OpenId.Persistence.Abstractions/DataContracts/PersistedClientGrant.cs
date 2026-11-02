// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Persistence.DataContracts;

/// <summary>
/// Contains the data for a persisted client grant (in Auth0 terms): a client's authorization to a resource server
/// with a set of granted scopes.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class PersistedClientGrant : ISupportTenantId, ISupportConcurrencyToken
{
    /// <summary>
    /// Gets the identifier of the tenant that owns this client grant.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the authorized client.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client is authorized to.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    public required string ResourceServerId { get; init; }

    /// <inheritdoc />
    [MaxLength(MaxLengths.ConcurrencyToken)]
    public required string ConcurrencyToken { get; set; }

    /// <summary>
    /// Gets or sets the granted scope values.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; set; }
}
