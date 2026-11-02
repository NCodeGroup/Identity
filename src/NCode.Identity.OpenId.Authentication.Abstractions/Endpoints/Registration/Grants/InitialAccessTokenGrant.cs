// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;

/// <summary>
/// Represents the payload of a persisted initial access token (RFC 7591). The token is persisted keyed by its value
/// and, while active, authorizes its bearer to register a new client at the dynamic client registration endpoint.
/// </summary>
[PublicAPI]
public readonly record struct InitialAccessTokenGrant
{
    /// <summary>
    /// Gets the optional human-readable description of the initial access token, for administrative purposes.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the identifier of the resource server the token is scoped to, or <c>null</c> when it registers ordinary
    /// clients. A client registered with a scoped token is bound to that resource server and introspects for it.
    /// </summary>
    public string? ResourceServerId { get; init; }
}
