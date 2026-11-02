// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jwk;

namespace NCode.Identity.Jose.DPoP;

/// <summary>
/// Represents a DPoP proof whose structure, signature, HTTP binding, and freshness are valid (RFC 9449 §4.3).
/// </summary>
[PublicAPI]
public sealed class DPoPProof
{
    /// <summary>
    /// Gets the JWS <c>alg</c> the proof is signed with.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the public key from the proof's <c>jwk</c> header.
    /// </summary>
    public required JsonWebKey PublicKey { get; init; }

    /// <summary>
    /// Gets the base64url SHA-256 JWK thumbprint (RFC 7638) of <see cref="PublicKey"/>: the <c>jkt</c> value an
    /// access token's <c>cnf</c> claim binds to.
    /// </summary>
    public required string JwkThumbprint { get; init; }

    /// <summary>
    /// Gets the proof's <c>jti</c> claim, which identifies the proof for replay detection.
    /// </summary>
    public required string Jti { get; init; }

    /// <summary>
    /// Gets the proof's <c>iat</c> claim.
    /// </summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// Gets the proof's <c>nonce</c> claim, if any.
    /// </summary>
    public string? Nonce { get; init; }
}
