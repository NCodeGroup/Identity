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
using NCode.Identity.Jwt.Validation;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// The known machine-readable codes of an invalid ID token or logout token.
/// </summary>
/// <remarks>
/// The codes are an open vocabulary: a code that the <c>JSON Web Token (JWT)</c> validator reports passes through
/// unchanged, so a result may carry a code that is not listed here. Codes are compared ordinally.
/// </remarks>
[PublicAPI]
public static class TokenValidationFailureCodes
{
    /// <summary>
    /// The token is malformed or structurally unacceptable.
    /// </summary>
    public const string InvalidToken = JwtValidationFailureCodes.InvalidToken;

    /// <summary>
    /// A header names an algorithm outside the allow-list, or the <c>alg</c> header is <c>none</c>.
    /// </summary>
    public const string AlgorithmNotPermitted = JwtValidationFailureCodes.AlgorithmNotPermitted;

    /// <summary>
    /// No candidate key verified the signature of the token.
    /// </summary>
    public const string InvalidSignature = JwtValidationFailureCodes.InvalidSignature;

    /// <summary>
    /// No key of the client decrypted the encrypted token.
    /// </summary>
    public const string DecryptionFailed = JwtValidationFailureCodes.DecryptionFailed;

    /// <summary>
    /// The <c>iss</c> claim is not the issuer of the provider.
    /// </summary>
    public const string InvalidIssuer = JwtValidationFailureCodes.InvalidIssuer;

    /// <summary>
    /// The <c>aud</c> claim does not name the client; this also covers an <c>azp</c> claim that names another party.
    /// </summary>
    public const string InvalidAudience = JwtValidationFailureCodes.InvalidAudience;

    /// <summary>
    /// The <c>exp</c> claim has passed, beyond the clock skew.
    /// </summary>
    public const string Expired = JwtValidationFailureCodes.Expired;

    /// <summary>
    /// The <c>nbf</c> or <c>iat</c> claim is in the future, beyond the clock skew.
    /// </summary>
    public const string NotYetValid = JwtValidationFailureCodes.NotYetValid;

    /// <summary>
    /// A lifetime claim is malformed or missing when required, <c>nbf</c> is after <c>exp</c>, or the lifetime exceeds
    /// the maximum.
    /// </summary>
    public const string InvalidLifetime = JwtValidationFailureCodes.InvalidLifetime;

    /// <summary>
    /// The <c>typ</c> header is missing or is not an allowed token type.
    /// </summary>
    public const string InvalidTokenType = JwtValidationFailureCodes.InvalidTokenType;

    /// <summary>
    /// A required claim is missing or malformed, or a prohibited claim is present.
    /// </summary>
    public const string InvalidClaim = JwtValidationFailureCodes.InvalidClaim;

    /// <summary>
    /// The <c>sub</c> claim is not the expected subject.
    /// </summary>
    public const string SubjectMismatch = "subject_mismatch";

    /// <summary>
    /// The <c>nonce</c> claim is missing or is not the nonce of the request.
    /// </summary>
    public const string NonceMismatch = "nonce_mismatch";

    /// <summary>
    /// The end-user authenticated longer ago than the requested maximum age.
    /// </summary>
    public const string MaxAgeExceeded = "max_age_exceeded";

    /// <summary>
    /// An <c>at_hash</c>, <c>c_hash</c> or <c>s_hash</c> claim does not match the value it hashes.
    /// </summary>
    public const string TokenHashMismatch = "token_hash_mismatch";

    /// <summary>
    /// The logout token has already been received.
    /// </summary>
    public const string TokenReplayed = "token_replayed";
}
