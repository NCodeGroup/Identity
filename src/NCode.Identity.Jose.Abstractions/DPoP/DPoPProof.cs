#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
