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
/// Represents an OAuth 2.0 token exchange request (RFC 8693).
/// </summary>
/// <param name="subjectToken">The token that represents the identity of the party on behalf of whom the request is made.</param>
/// <param name="subjectTokenType">The type identifier of the subject token.
/// See <see cref="OpenIdProtocol.TokenTypeIdentifiers"/> for the known values.</param>
[PublicAPI]
public sealed class TokenExchangeTokenRequest(string subjectToken, string subjectTokenType)
    : TokenRequest
{
    /// <inheritdoc />
    public override string GrantType => OpenIdProtocol.GrantTypes.TokenExchange;

    /// <summary>
    /// Gets the subject token.
    /// </summary>
    public string SubjectToken { get; } = ValidateNotEmpty(subjectToken);

    /// <summary>
    /// Gets the type identifier of the subject token.
    /// </summary>
    public string SubjectTokenType { get; } = ValidateNotEmpty(subjectTokenType);

    /// <summary>
    /// Gets the optional actor token.
    /// </summary>
    public string? ActorToken { get; init; }

    /// <summary>
    /// Gets the optional type identifier of the actor token.
    /// </summary>
    public string? ActorTokenType { get; init; }

    /// <summary>
    /// Gets the optional type identifier of the requested token.
    /// </summary>
    public string? RequestedTokenType { get; init; }

    /// <summary>
    /// Gets the logical names of the target services, each written as its own <c>audience</c> parameter.
    /// </summary>
    public List<string> Audiences { get; init; } = [];

    /// <inheritdoc />
    public override void AddGrantParameters(ICollection<KeyValuePair<string, string>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.Add(
            new KeyValuePair<string, string>(OpenIdProtocol.Parameters.SubjectToken, SubjectToken)
        );
        parameters.Add(
            new KeyValuePair<string, string>(
                OpenIdProtocol.Parameters.SubjectTokenType,
                SubjectTokenType
            )
        );

        if (ActorToken is not null)
            parameters.Add(
                new KeyValuePair<string, string>(OpenIdProtocol.Parameters.ActorToken, ActorToken)
            );

        if (ActorTokenType is not null)
            parameters.Add(
                new KeyValuePair<string, string>(
                    OpenIdProtocol.Parameters.ActorTokenType,
                    ActorTokenType
                )
            );

        if (RequestedTokenType is not null)
            parameters.Add(
                new KeyValuePair<string, string>(
                    OpenIdProtocol.Parameters.RequestedTokenType,
                    RequestedTokenType
                )
            );

        foreach (var audience in Audiences)
            parameters.Add(
                new KeyValuePair<string, string>(OpenIdProtocol.Parameters.Audience, audience)
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
