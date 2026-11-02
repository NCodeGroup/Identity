// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// The known machine-readable codes of an invalid UserInfo response. A signed or encrypted response that fails its
/// validation reports a <see cref="TokenValidationFailureCodes"/> value instead.
/// </summary>
/// <remarks>
/// The codes are an open vocabulary, compared ordinally.
/// </remarks>
[PublicAPI]
public static class UserInfoFailureCodes
{
    /// <summary>
    /// The response is about another subject than the ID token (OpenID Connect Core 1.0 §5.3.2), so its claims must
    /// not be used.
    /// </summary>
    public const string SubjectMismatch = TokenValidationFailureCodes.SubjectMismatch;

    /// <summary>
    /// The response's <c>iss</c> member names another issuer than the provider's, so its claims would be attributed
    /// to that issuer.
    /// </summary>
    public const string InvalidIssuer = TokenValidationFailureCodes.InvalidIssuer;

    /// <summary>
    /// The response's claims are not a JSON object with a string <c>sub</c>.
    /// </summary>
    public const string InvalidResponse = "invalid_response";

    /// <summary>
    /// The response is not in the format the client registered: a JWT (<c>application/jwt</c>) when the client
    /// registered no UserInfo signing, or JSON when it did (<see cref="ClientRegistration.UserInfoSigningAlgorithms"/>).
    /// </summary>
    public const string UnsupportedResponse = "unsupported_response";
}
