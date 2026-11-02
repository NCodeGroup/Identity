// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the REST resource for a client grant (in Auth0 terms): a client's authorization to a resource server
/// with a set of granted scopes.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientGrantResource : ISupportTenantId, ISupportConcurrencyToken
{
    /// <inheritdoc cref="ISupportTenantId.TenantId"/>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the identifier of the authorized client.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the client is authorized to.
    /// </summary>
    public required string ResourceServerId { get; init; }

    /// <inheritdoc cref="ISupportConcurrencyToken.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <summary>
    /// Gets the granted scope values.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }
}
