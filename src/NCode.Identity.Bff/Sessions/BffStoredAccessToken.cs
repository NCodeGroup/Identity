// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// An access token that the session caches, as stored in the encrypted session payload.
/// </summary>
internal sealed record BffStoredAccessToken
{
    /// <summary>
    /// Gets the name of the token requirement that the token satisfies.
    /// </summary>
    public string Requirement { get; init; } = string.Empty;

    /// <summary>
    /// Gets the resource indicator that the token was requested for, if any.
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// Gets the scope that the token was requested with, if any.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the access token value.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Gets the token type, such as <c>Bearer</c> or <c>DPoP</c>.
    /// </summary>
    public string TokenType { get; init; } = string.Empty;

    /// <summary>
    /// Gets the instant when the token expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the JWK thumbprint that the token is bound to, if it is DPoP-bound.
    /// </summary>
    public string? DPoPJwkThumbprint { get; init; }
}
