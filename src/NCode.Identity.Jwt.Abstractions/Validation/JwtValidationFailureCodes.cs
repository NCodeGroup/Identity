#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// The known codes of a <c>JSON Web Token (JWT)</c> validation failure. Carried on
/// <see cref="JwtValidationException.FailureCode"/> so a caller can tell why a token failed without matching on the
/// text of the failure reason.
/// </summary>
/// <remarks>
/// The codes are an open vocabulary: a validation handler or requirement that fails a token for its own cause passes a
/// code of its own. Codes are compared ordinally.
/// </remarks>
[PublicAPI]
public static class JwtValidationFailureCodes
{
    /// <summary>
    /// The token is malformed or structurally unacceptable; this is the default code.
    /// </summary>
    public const string InvalidToken = "invalid_token";

    /// <summary>
    /// A header names an algorithm outside the allow-list, or the <c>alg</c> header is <c>none</c>.
    /// </summary>
    public const string AlgorithmNotPermitted = "algorithm_not_permitted";

    /// <summary>
    /// No candidate key exists to verify or decrypt the token with.
    /// </summary>
    public const string KeyNotFound = "key_not_found";

    /// <summary>
    /// No candidate key verified the signature of the token.
    /// </summary>
    public const string InvalidSignature = "invalid_signature";

    /// <summary>
    /// No candidate key decrypted the token.
    /// </summary>
    public const string DecryptionFailed = "decryption_failed";

    /// <summary>
    /// A claim-value requirement failed; this is the default code of a claim-value requirement.
    /// </summary>
    public const string InvalidClaim = "invalid_claim";

    /// <summary>
    /// The <c>iss</c> requirement failed.
    /// </summary>
    public const string InvalidIssuer = "invalid_issuer";

    /// <summary>
    /// The <c>aud</c> requirement failed.
    /// </summary>
    public const string InvalidAudience = "invalid_audience";

    /// <summary>
    /// The <c>typ</c> header is missing or is not an allowed token type.
    /// </summary>
    public const string InvalidTokenType = "invalid_token_type";

    /// <summary>
    /// The <c>exp</c> claim has passed, beyond the clock skew.
    /// </summary>
    public const string Expired = "expired";

    /// <summary>
    /// The <c>nbf</c> or <c>iat</c> claim is in the future, beyond the clock skew.
    /// </summary>
    public const string NotYetValid = "not_yet_valid";

    /// <summary>
    /// A lifetime claim is malformed or missing when required, <c>nbf</c> is after <c>exp</c>, or the lifetime exceeds
    /// the maximum.
    /// </summary>
    public const string InvalidLifetime = "invalid_lifetime";

    /// <summary>
    /// The signing certificate is outside its validity period.
    /// </summary>
    public const string InvalidCertificate = "invalid_certificate";

    /// <summary>
    /// An assertion requirement returned <see langword="false"/>.
    /// </summary>
    public const string AssertionFailed = "assertion_failed";
}
