// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// What a verifier requires of the Key Binding JWT of a presentation (RFC 9901 §7.3): the audience and nonce it
/// supplied, the signature algorithms it accepts, and how fresh the Key Binding JWT must be. The verifier refuses a
/// requirement whose audience or nonce is empty, whose algorithms are unset, whose maximum age is not positive or
/// whose clock skew is negative before it reads the presentation, so a requirement built from a missing value can
/// never let a captured Key Binding JWT replay.
/// </summary>
[PublicAPI]
public sealed class SdJwtKeyBindingRequirement
{
    /// <summary>
    /// Gets the verifier's own identifier, which the Key Binding JWT's <c>aud</c> claim must name. It must not be
    /// empty.
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>
    /// Gets the nonce the verifier supplied for this presentation, which the Key Binding JWT's <c>nonce</c> claim must
    /// equal. It must not be empty.
    /// </summary>
    public required string Nonce { get; init; }

    /// <summary>
    /// Gets the JWS <c>alg</c> values the Key Binding JWT may be signed with; an empty set permits none. Only an
    /// asymmetric algorithm ever verifies, because the holder key is public.
    /// </summary>
    public required IReadOnlyCollection<string> ValidSignatureAlgorithms { get; init; }

    /// <summary>
    /// Gets how old the Key Binding JWT's <c>iat</c> claim may be, beyond <see cref="ClockSkew"/>. It must be
    /// positive.
    /// </summary>
    public required TimeSpan MaxAge { get; init; }

    /// <summary>
    /// Gets how far into the future the Key Binding JWT's <c>iat</c> claim may lie, and the skew allowed beyond
    /// <see cref="MaxAge"/>. It must not be negative. The default value is five minutes.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(5);
}
