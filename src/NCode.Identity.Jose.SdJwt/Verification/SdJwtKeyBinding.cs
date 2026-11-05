// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jwt;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// The verified Key Binding JWT of a presentation (RFC 9901 §4.3): the holder key from the <c>cnf</c> claim that
/// signed it, and the claims the verifier checked.
/// </summary>
[PublicAPI]
public sealed class SdJwtKeyBinding
{
    /// <summary>
    /// Gets the decoded Key Binding JWT.
    /// </summary>
    public required DecodedJwt Jwt { get; init; }

    /// <summary>
    /// Gets the holder key, resolved from the issuer-signed JWT's <c>cnf</c> claim, that verified the signature.
    /// </summary>
    public required SecretKey HolderKey { get; init; }

    /// <summary>
    /// Gets the <c>nonce</c> claim.
    /// </summary>
    public required string Nonce { get; init; }

    /// <summary>
    /// Gets the <c>iat</c> claim.
    /// </summary>
    public required DateTimeOffset IssuedAt { get; init; }
}
