#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Endpoints.Token.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenResponse"/> abstraction.
/// </summary>
[PublicAPI]
public class TokenResponse : OpenIdMessage<TokenResponse>, ITokenResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenResponse"/> class.
    /// </summary>
    public TokenResponse()
    {
        // nothing
    }

    /// <inheritdoc />
    protected TokenResponse(TokenResponse other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public string? AccessToken
    {
        get => GetKnownParameter(KnownParameters.AccessToken);
        set => SetKnownParameter(KnownParameters.AccessToken, value);
    }

    /// <inheritdoc />
    public TimeSpan? ExpiresIn
    {
        get => GetKnownParameter(KnownParameters.ExpiresIn);
        set => SetKnownParameter(KnownParameters.ExpiresIn, value);
    }

    /// <inheritdoc />
    public string? IdToken
    {
        get => GetKnownParameter(KnownParameters.IdToken);
        set => SetKnownParameter(KnownParameters.IdToken, value);
    }

    /// <inheritdoc />
    public string? RefreshToken
    {
        get => GetKnownParameter(KnownParameters.RefreshToken);
        set => SetKnownParameter(KnownParameters.RefreshToken, value);
    }

    /// <inheritdoc />
    public List<string>? Scopes
    {
        get => GetKnownParameter(KnownParameters.Scopes);
        set => SetKnownParameter(KnownParameters.Scopes, value);
    }

    /// <inheritdoc />
    public string? TokenType
    {
        get => GetKnownParameter(KnownParameters.TokenType);
        set => SetKnownParameter(KnownParameters.TokenType, value);
    }

    /// <inheritdoc />
    public override TokenResponse Clone() => new(this);
}
