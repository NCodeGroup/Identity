// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Grants;

/// <summary>
/// Represents the request to mint an initial access token (RFC 7591) that authorizes its bearer to register a new
/// client at the protected dynamic client registration endpoint.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateInitialAccessTokenRequest
{
    /// <summary>
    /// Gets the optional human-readable description of the initial access token, recorded for administrative purposes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the optional lifetime of the token, in seconds. When <c>null</c>, the token lives for one day. When
    /// specified, it must be greater than zero and at most 2 592 000 seconds (30 days).
    /// </summary>
    public long? LifetimeSeconds { get; init; }

    /// <summary>
    /// Gets the optional identifier of the resource server the token is scoped to. A client registered with a scoped
    /// token is bound to that resource server and introspects for it, so the resource server must exist in the tenant,
    /// must not be a system resource server, and the caller must be authorized to update it. When <c>null</c>, the
    /// token registers ordinary clients.
    /// </summary>
    public string? ResourceServerId { get; init; }
}
