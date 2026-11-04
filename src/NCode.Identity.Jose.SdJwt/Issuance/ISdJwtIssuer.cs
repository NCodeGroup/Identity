// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.SdJwt.Issuance;

/// <summary>
/// Issues Selective Disclosure JWTs (RFC 9901 §5): each disclosable claim is moved into a salted disclosure and its
/// digest written in its place, decoys are added where asked, the holder key is written to <c>cnf</c>, and the
/// payload is signed as a compact JWS.
/// </summary>
[PublicAPI]
public interface ISdJwtIssuer
{
    /// <summary>
    /// Issues a Selective Disclosure JWT.
    /// </summary>
    /// <param name="options">The claims, the disclosable claims and the signing key.</param>
    /// <returns>The issued SD-JWT with every disclosure, whose <see cref="SelectiveDisclosureJwt.ToString"/> is the
    /// combined format handed to the holder.</returns>
    /// <exception cref="ArgumentException">The options are not usable: the signing key is not asymmetric, the claims
    /// are not an object or use a reserved name, a disclosable path is malformed or names nothing, a profile
    /// requirement is not met, the digest algorithm is not registered, or the holder key carries private material.</exception>
    SelectiveDisclosureJwt Issue(SdJwtIssuanceOptions options);
}
