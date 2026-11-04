// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Profiles;

/// <summary>
/// A profile of the Selective Disclosure JWT (SD-JWT) format, such as SD-JWT VC, expressed as data: the <c>typ</c> of
/// the issuer-signed JWT, the claims that must be present in its payload as non-empty strings, and the claims that are
/// never selectively disclosable. The issuer applies it when it issues and the verifier when it verifies, so the two
/// agree on one definition.
/// </summary>
/// <param name="name">The name of the profile.</param>
/// <param name="tokenType">The <c>typ</c> JOSE header value of the issuer-signed JWT, matched by the media-type rules
/// of RFC 7515 §4.1.9.</param>
/// <param name="requiredClaims">The names of the claims that must be present in the issuer-signed JWT payload as
/// non-empty strings.</param>
/// <param name="protectedClaims">The names of the top-level claims that are never selectively disclosable: an issuer
/// refuses to make one disclosable, and a verifier refuses a disclosure that discloses one.</param>
// See ADR-1406.
[PublicAPI]
public sealed class SdJwtProfile(
    string name,
    string tokenType,
    IEnumerable<string> requiredClaims,
    IEnumerable<string> protectedClaims
)
{
    /// <summary>
    /// Gets the name of the profile.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the <c>typ</c> JOSE header value of the issuer-signed JWT.
    /// </summary>
    public string TokenType { get; } = tokenType;

    /// <summary>
    /// Gets the names of the claims that must be present in the issuer-signed JWT payload as non-empty strings.
    /// </summary>
    public FrozenSet<string> RequiredClaims { get; } =
        requiredClaims.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Gets the names of the top-level claims that are never selectively disclosable.
    /// </summary>
    public FrozenSet<string> ProtectedClaims { get; } =
        protectedClaims.ToFrozenSet(StringComparer.Ordinal);
}
