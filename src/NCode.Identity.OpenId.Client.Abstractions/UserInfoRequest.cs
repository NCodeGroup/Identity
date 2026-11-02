// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents a UserInfo request (OpenID Connect Core 1.0 §5.3.1).
/// </summary>
/// <param name="accessToken">The access token to present.</param>
/// <param name="expectedSubject">The <c>sub</c> of the validated ID token, which the response must carry exactly
/// (OpenID Connect Core 1.0 §5.3.2).</param>
[PublicAPI]
public sealed class UserInfoRequest(string accessToken, string expectedSubject)
{
    /// <summary>
    /// Gets the access token to present.
    /// </summary>
    public string AccessToken { get; } = ValidateNotEmpty(accessToken);

    /// <summary>
    /// Gets the <c>sub</c> of the validated ID token, which the response must carry exactly.
    /// </summary>
    public string ExpectedSubject { get; } = ValidateNotEmpty(expectedSubject);

    /// <summary>
    /// Gets the <c>jkt</c> of the DPoP key the access token is bound to, which <see cref="TokenResult.DPoPJwkThumbprint"/>
    /// reported, or <see langword="null"/> for a bearer token. A DPoP-bound token is presented with a proof of that key.
    /// </summary>
    public string? DPoPJwkThumbprint { get; init; }

    private static string ValidateNotEmpty(
        string value,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))]
            string? paramName = null
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }
}
