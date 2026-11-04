// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.SdJwt.Profiles;
using NCode.Identity.Jwt.Validation;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// The policy a presentation of a Selective Disclosure JWT is verified against (RFC 9901 §7).
/// </summary>
[PublicAPI]
public sealed class SdJwtVerificationParameters
{
    /// <summary>
    /// Gets the whole policy of the issuer-signed JWT: the issuer's keys, the signature algorithms it may use, which
    /// must be set, and the issuer, lifetime and claim requirements. An encrypted issuer-signed JWT is never accepted.
    /// </summary>
    public required JwtValidationParameters IssuerJwtParameters { get; init; }

    /// <summary>
    /// Gets what the verifier requires of the Key Binding JWT, or <c>null</c> when the verifier requires no key
    /// binding, in which case a Key Binding JWT the presentation carries is not verified.
    /// </summary>
    public SdJwtKeyBindingRequirement? KeyBinding { get; init; }

    /// <summary>
    /// Gets the profile the issuer-signed JWT must conform to, such as <see cref="SdJwtProfiles.SdJwtVc"/>, or
    /// <c>null</c> for a plain SD-JWT.
    /// </summary>
    public SdJwtProfile? Profile { get; init; }
}
