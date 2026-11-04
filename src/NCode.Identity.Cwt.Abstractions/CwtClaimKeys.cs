// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cwt;

/// <summary>
/// Contains the integer keys of the CWT claims this library knows (RFC 8392 §4, RFC 8747, RFC 9200). The keys are the
/// known ones, never all of them: a claims set carries any integer or text key.
/// </summary>
[PublicAPI]
public static class CwtClaimKeys
{
    /// <summary>
    /// The <c>iss</c> claim, the issuer.
    /// </summary>
    public const long Issuer = 1;

    /// <summary>
    /// The <c>sub</c> claim, the subject.
    /// </summary>
    public const long Subject = 2;

    /// <summary>
    /// The <c>aud</c> claim, the audience.
    /// </summary>
    public const long Audience = 3;

    /// <summary>
    /// The <c>exp</c> claim, the expiration time.
    /// </summary>
    public const long ExpirationTime = 4;

    /// <summary>
    /// The <c>nbf</c> claim, the time before which the token is not valid.
    /// </summary>
    public const long NotBefore = 5;

    /// <summary>
    /// The <c>iat</c> claim, the time the token was issued.
    /// </summary>
    public const long IssuedAt = 6;

    /// <summary>
    /// The <c>cti</c> claim, the token identifier.
    /// </summary>
    public const long CwtId = 7;

    /// <summary>
    /// The <c>cnf</c> claim, the confirmation method (RFC 8747).
    /// </summary>
    public const long Confirmation = 8;

    /// <summary>
    /// The <c>scope</c> claim (RFC 9200).
    /// </summary>
    public const long Scope = 9;
}
