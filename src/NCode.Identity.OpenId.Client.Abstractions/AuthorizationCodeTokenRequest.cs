// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents an authorization code grant request with a PKCE code verifier.
/// </summary>
/// <param name="code">The authorization code.</param>
/// <param name="redirectUri">The redirect URI used in the authorization request.</param>
/// <param name="codeVerifier">The PKCE code verifier.</param>
[PublicAPI]
public sealed class AuthorizationCodeTokenRequest(
    string code,
    string redirectUri,
    string codeVerifier
) : TokenRequest
{
    /// <inheritdoc />
    public override string GrantType => OpenIdProtocol.GrantTypes.AuthorizationCode;

    /// <summary>
    /// Gets the authorization code.
    /// </summary>
    public string Code { get; } = ValidateNotEmpty(code);

    /// <summary>
    /// Gets the redirect URI used in the authorization request.
    /// </summary>
    public string RedirectUri { get; } = ValidateNotEmpty(redirectUri);

    /// <summary>
    /// Gets the PKCE code verifier.
    /// </summary>
    public string CodeVerifier { get; } = ValidateNotEmpty(codeVerifier);

    /// <inheritdoc />
    public override void AddGrantParameters(ICollection<KeyValuePair<string, string>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Add(
            new KeyValuePair<string, string>(OpenIdProtocol.Parameters.AuthorizationCode, Code)
        );
        parameters.Add(
            new KeyValuePair<string, string>(OpenIdProtocol.Parameters.RedirectUri, RedirectUri)
        );
        parameters.Add(
            new KeyValuePair<string, string>(OpenIdProtocol.Parameters.CodeVerifier, CodeVerifier)
        );
    }

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
