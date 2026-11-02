// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the request body to create a client grant: a client's authorization to a resource server with a set of
/// granted scopes.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateClientGrantRequest
{
    /// <summary>
    /// Gets the identifier of the resource server the client is authorized to.
    /// </summary>
    public required string ResourceServerId { get; init; }

    /// <summary>
    /// Gets the granted scope values. Each must be a scope defined on the target resource server.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }
}
