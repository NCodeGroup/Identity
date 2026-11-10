// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.Jwt;

/// <summary>
/// Provides the optional inputs of a JWT that a test encodes.
/// </summary>
public sealed class JwtTestTokenOptions
{
    /// <summary>
    /// Gets the issuer of the token, or <see langword="null"/> for the default issuer of the tests.
    /// </summary>
    public string? Issuer { get; init; }

    /// <summary>
    /// Gets the audiences of the token, or <see langword="null"/> for the default audience of the tests.
    /// </summary>
    public string[]? Audience { get; init; }

    /// <summary>
    /// Gets the not-before time of the token, or <see langword="null"/> for a minute before the issue time.
    /// </summary>
    public DateTimeOffset? NotBefore { get; init; }

    /// <summary>
    /// Gets the expiration time of the token, or <see langword="null"/> for an hour after the issue time.
    /// </summary>
    public DateTimeOffset? Expires { get; init; }

    /// <summary>
    /// Gets the subject claims of the token, or <see langword="null"/> for none.
    /// </summary>
    public IEnumerable<Claim>? SubjectClaims { get; init; }

    /// <summary>
    /// Gets the token type of the token, or <see langword="null"/> for a JWT.
    /// </summary>
    public string? TokenType { get; init; }

    /// <summary>
    /// Gets the extra payload claims of the token, or <see langword="null"/> for none.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraPayloadClaims { get; init; }
}
