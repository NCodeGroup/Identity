// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents a token revocation request (RFC 7009 §2.1).
/// </summary>
/// <param name="token">The token to revoke.</param>
[PublicAPI]
public sealed class TokenRevocationRequest(string token)
{
    /// <summary>
    /// Gets the token to revoke.
    /// </summary>
    public string Token { get; } = ValidateNotEmpty(token);

    /// <summary>
    /// Gets the hint about the type of the token, if any. See <see cref="OpenIdProtocol.TokenTypeHints"/>.
    /// </summary>
    public string? TokenTypeHint { get; init; }

    private static string ValidateNotEmpty(
        string value,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))]
            string? paramName = null
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }
}
