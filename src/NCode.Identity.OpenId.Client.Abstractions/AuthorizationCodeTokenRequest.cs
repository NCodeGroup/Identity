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
