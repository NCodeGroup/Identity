// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cwt.Validation;

/// <summary>
/// Contains the machine-readable codes by which CWT validation fails. A failure of the COSE layer reports its
/// <see cref="Cose.CoseFailureCodes"/> code instead. The codes are the known ones, never all of them. A code that names
/// the same concept as a JSON Web Token validation code uses the same string.
/// </summary>
[PublicAPI]
public static class CwtValidationFailureCodes
{
    /// <summary>
    /// The token has a shape that is not accepted, such as an encryption-only token.
    /// </summary>
    public const string InvalidToken = "invalid_token";

    /// <summary>
    /// The token does not carry the expected type.
    /// </summary>
    public const string InvalidTokenType = "invalid_token_type";

    /// <summary>
    /// A claim has a form that RFC 8392 does not allow.
    /// </summary>
    public const string InvalidClaim = "invalid_claim";

    /// <summary>
    /// A required claim is absent.
    /// </summary>
    public const string MissingClaim = "missing_claim";

    /// <summary>
    /// The issuer is not one the verifier accepts.
    /// </summary>
    public const string InvalidIssuer = "invalid_issuer";

    /// <summary>
    /// The audience of the token does not match the audiences the verifier names.
    /// </summary>
    public const string InvalidAudience = "invalid_audience";

    /// <summary>
    /// The token has expired.
    /// </summary>
    public const string Expired = "expired";

    /// <summary>
    /// The token is not yet valid.
    /// </summary>
    public const string NotYetValid = "not_yet_valid";

    /// <summary>
    /// The lifetime of the token is inconsistent or exceeds the maximum.
    /// </summary>
    public const string InvalidLifetime = "invalid_lifetime";
}
