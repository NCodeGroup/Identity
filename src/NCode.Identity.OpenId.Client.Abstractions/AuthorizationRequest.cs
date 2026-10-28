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
/// Represents an authorization code request with PKCE <c>S256</c> (RFC 6749 section 4.1.1, RFC 7636). The client
/// writes <c>response_type=code</c>, <c>client_id</c> and the <c>S256</c> code challenge itself.
/// </summary>
/// <param name="redirectUri">The <c>redirect_uri</c> the provider redirects the user agent back to.</param>
/// <param name="state">The <c>state</c> value that binds the response to the request.</param>
/// <param name="codeVerifier">The PKCE code verifier from which the <c>code_challenge</c> is derived.</param>
[PublicAPI]
public sealed class AuthorizationRequest(string redirectUri, string state, string codeVerifier)
{
    private static readonly HashSet<string> ReservedParameterNames = new(StringComparer.Ordinal)
    {
        OpenIdProtocol.Parameters.ResponseType,
        OpenIdProtocol.Parameters.ClientId,
        OpenIdProtocol.Parameters.RedirectUri,
        OpenIdProtocol.Parameters.State,
        OpenIdProtocol.Parameters.Nonce,
        OpenIdProtocol.Parameters.Scope,
        OpenIdProtocol.Parameters.Resource,
        OpenIdProtocol.Parameters.ResponseMode,
        OpenIdProtocol.Parameters.CodeChallenge,
        OpenIdProtocol.Parameters.CodeChallengeMethod,
        OpenIdProtocol.Parameters.RequestUri,
        OpenIdProtocol.Parameters.ClientSecret,
        OpenIdProtocol.Parameters.ClientAssertion,
        OpenIdProtocol.Parameters.ClientAssertionType,
    };

    private IReadOnlyDictionary<string, string>? _additionalParameters;

    /// <summary>
    /// Gets the <c>redirect_uri</c> parameter.
    /// </summary>
    public string RedirectUri { get; } = ValidateNotEmpty(redirectUri);

    /// <summary>
    /// Gets the <c>state</c> parameter.
    /// </summary>
    public string State { get; } = ValidateNotEmpty(state);

    /// <summary>
    /// Gets the PKCE code verifier from which the <c>code_challenge</c> parameter is derived (RFC 7636 section 4.2).
    /// </summary>
    public string CodeVerifier { get; } = ValidateCodeVerifier(codeVerifier);

    /// <summary>
    /// Gets the <c>nonce</c> parameter, which binds the ID token to the request.
    /// </summary>
    public string? Nonce { get; init; }

    /// <summary>
    /// Gets the <c>scope</c> parameter, a space-delimited list of scope values.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the <c>response_mode</c> parameter.
    /// </summary>
    public string? ResponseMode { get; init; }

    /// <summary>
    /// Gets the <c>resource</c> parameters (RFC 8707), one per resource indicator.
    /// </summary>
    public List<string> Resources { get; init; } = [];

    /// <summary>
    /// Gets extension parameters such as <c>prompt</c>, <c>max_age</c>, <c>login_hint</c> or <c>dpop_jkt</c>.
    /// Names the client writes itself are rejected with <see cref="ArgumentException"/>. The dictionary is copied.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalParameters
    {
        get => _additionalParameters;
        init => _additionalParameters = ValidateAdditionalParameters(value);
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

    private static string ValidateCodeVerifier(string codeVerifier)
    {
        ArgumentNullException.ThrowIfNull(codeVerifier);

        if (codeVerifier.Length is < 43 or > 128 || !codeVerifier.All(IsUnreserved))
        {
            throw new ArgumentException(
                "The PKCE code verifier must be 43 to 128 characters from A-Z, a-z, 0-9, '-', '.', '_' and '~' (RFC 7636 §4.1).",
                nameof(codeVerifier)
            );
        }

        return codeVerifier;

        static bool IsUnreserved(char c) =>
            char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~';
    }

    private static Dictionary<string, string>? ValidateAdditionalParameters(
        IReadOnlyDictionary<string, string>? value
    )
    {
        if (value is null)
            return null;

        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, parameterValue) in value)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException(
                    "An additional authorization parameter has an empty name.",
                    nameof(value)
                );

            if (ReservedParameterNames.Contains(name))
            {
                throw new ArgumentException(
                    $"The authorization parameter '{name}' is written by the client and cannot be an additional parameter.",
                    nameof(value)
                );
            }

            copy[name] = parameterValue;
        }

        return copy;
    }
}
