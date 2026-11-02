// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the request body to replace the granted scopes of a client grant.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateClientGrantRequest
{
    /// <summary>
    /// Gets the granted scope values. Each must be a scope defined on the target resource server.
    /// </summary>
    public required IReadOnlyList<string> Scopes { get; init; }
}
