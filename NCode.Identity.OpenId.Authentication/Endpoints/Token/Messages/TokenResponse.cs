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

using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Messages;

/// <summary>
/// Provides a default implementation of the <see cref="ITokenResponse"/> abstraction.
/// </summary>
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
        get => GetKnownParameter(CoreParameters.AccessToken);
        set => SetKnownParameter(CoreParameters.AccessToken, value);
    }

    /// <inheritdoc />
    public TimeSpan? ExpiresIn
    {
        get => GetKnownParameter(CoreParameters.ExpiresIn);
        set => SetKnownParameter(CoreParameters.ExpiresIn, value);
    }

    /// <inheritdoc />
    public string? IdToken
    {
        get => GetKnownParameter(CoreParameters.IdToken);
        set => SetKnownParameter(CoreParameters.IdToken, value);
    }

    /// <inheritdoc />
    public string? RefreshToken
    {
        get => GetKnownParameter(CoreParameters.RefreshToken);
        set => SetKnownParameter(CoreParameters.RefreshToken, value);
    }

    /// <inheritdoc />
    public List<string>? Scopes
    {
        get => GetKnownParameter(CoreParameters.Scopes);
        set => SetKnownParameter(CoreParameters.Scopes, value);
    }

    /// <inheritdoc />
    public string? TokenType
    {
        get => GetKnownParameter(CoreParameters.TokenType);
        set => SetKnownParameter(CoreParameters.TokenType, value);
    }

    /// <inheritdoc />
    public override TokenResponse Clone() => new(this);
}
