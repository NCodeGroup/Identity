#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
/// Provides a default implementation of the <see cref="ITokenRequest"/> abstraction.
/// </summary>
public class TokenRequest : OpenIdMessage<TokenRequest>, ITokenRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenRequest"/> class.
    /// </summary>
    public TokenRequest()
    {
        // nothing
    }

    /// <inheritdoc />
    protected TokenRequest(TokenRequest other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    public string? AuthorizationCode
    {
        get => GetKnownParameter(CoreParameters.AuthorizationCode);
        set => SetKnownParameter(CoreParameters.AuthorizationCode, value);
    }

    /// <inheritdoc />
    public string? ClientId
    {
        get => GetKnownParameter(CoreParameters.ClientId);
        set => SetKnownParameter(CoreParameters.ClientId, value);
    }

    /// <inheritdoc />
    public string? CodeVerifier
    {
        get => GetKnownParameter(CoreParameters.CodeVerifier);
        set => SetKnownParameter(CoreParameters.CodeVerifier, value);
    }

    /// <inheritdoc />
    public string? GrantType
    {
        get => GetKnownParameter(CoreParameters.GrantType);
        set => SetKnownParameter(CoreParameters.GrantType, value);
    }

    /// <inheritdoc />
    public string? Password
    {
        get => GetKnownParameter(CoreParameters.Password);
        set => SetKnownParameter(CoreParameters.Password, value);
    }

    /// <inheritdoc />
    public Uri? RedirectUri
    {
        get => GetKnownParameter(CoreParameters.RedirectUri);
        set => SetKnownParameter(CoreParameters.RedirectUri, value);
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
    public string? Username
    {
        get => GetKnownParameter(CoreParameters.Username);
        set => SetKnownParameter(CoreParameters.Username, value);
    }

    /// <inheritdoc />
    public override TokenRequest Clone() => new(this);
}
