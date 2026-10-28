#region Copyright Preamble

// Copyright @ 2024 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Tokens.Models;

/// <summary>
/// Represents the <c>cnf</c> (confirmation) claim that binds an issued token to a proof-of-possession key
/// (<see href="https://datatracker.ietf.org/doc/html/rfc7800">RFC 7800</see>). A DPoP binding supplies the
/// <see cref="JwkThumbprint"/> (<c>jkt</c>); a mutual-TLS binding supplies the
/// <see cref="X509CertificateSha256Thumbprint"/> (<c>x5t#S256</c>).
/// </summary>
[PublicAPI]
public readonly record struct TokenConfirmation
{
    /// <summary>
    /// Gets an empty <see cref="TokenConfirmation"/> that binds a token to no proof-of-possession key.
    /// </summary>
    public static TokenConfirmation None => default;

    /// <summary>
    /// Gets the base64url-encoded SHA-256 JWK thumbprint (RFC 7638) of the DPoP public key the token is bound to, or
    /// <see langword="null"/> when the token is not DPoP-bound.
    /// </summary>
    public string? JwkThumbprint { get; init; }

    /// <summary>
    /// Gets the base64url-encoded SHA-256 thumbprint of the client certificate the token is bound to (RFC 8705), or
    /// <see langword="null"/> when the token is not certificate-bound.
    /// </summary>
    public string? X509CertificateSha256Thumbprint { get; init; }

    /// <summary>
    /// Gets a value indicating whether this confirmation binds the token to at least one proof-of-possession key.
    /// </summary>
    public bool HasValue =>
        !string.IsNullOrEmpty(JwkThumbprint)
        || !string.IsNullOrEmpty(X509CertificateSha256Thumbprint);

    /// <summary>
    /// Builds the JSON object value of the <c>cnf</c> claim from the populated confirmation members, or
    /// <see langword="null"/> when <see cref="HasValue"/> is <see langword="false"/>.
    /// </summary>
    /// <returns>A dictionary carrying the populated <c>cnf</c> members, or <see langword="null"/> when none are set.</returns>
    public IReadOnlyDictionary<string, object>? ToClaimValueOrNull()
    {
        if (!HasValue)
            return null;

        var members = new Dictionary<string, object>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(JwkThumbprint))
            members[OpenIdProtocol.ConfirmationMethods.JwkThumbprint] = JwkThumbprint;

        if (!string.IsNullOrEmpty(X509CertificateSha256Thumbprint))
            members[OpenIdProtocol.ConfirmationMethods.X509CertificateSha256Thumbprint] =
                X509CertificateSha256Thumbprint;

        return members;
    }
}
