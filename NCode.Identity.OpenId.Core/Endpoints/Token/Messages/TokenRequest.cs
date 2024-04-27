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

namespace NCode.Identity.OpenId.Endpoints.Token.Messages;

internal class TokenRequest : OpenIdMessage<TokenRequest>, ITokenRequest
{
    /// <inheritdoc />
    public string? ClientId
    {
        get => GetKnownParameter(KnownParameters.ClientId);
        set => SetKnownParameter(KnownParameters.ClientId, value);
    }

    /// <inheritdoc />
    public string? Code
    {
        get => GetKnownParameter(KnownParameters.Code);
        set => SetKnownParameter(KnownParameters.Code, value);
    }

    /// <inheritdoc />
    public string? GrantType
    {
        get => GetKnownParameter(KnownParameters.GrantType);
        set => SetKnownParameter(KnownParameters.GrantType, value);
    }

    /// <inheritdoc />
    public string? Password
    {
        get => GetKnownParameter(KnownParameters.Password);
        set => SetKnownParameter(KnownParameters.Password, value);
    }

    /// <inheritdoc />
    public Uri? RedirectUri
    {
        get => GetKnownParameter(KnownParameters.RedirectUri);
        set => SetKnownParameter(KnownParameters.RedirectUri, value);
    }

    /// <inheritdoc />
    public string? RefreshToken
    {
        get => GetKnownParameter(KnownParameters.RefreshToken);
        set => SetKnownParameter(KnownParameters.RefreshToken, value);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string>? Scopes
    {
        get => GetKnownParameter(KnownParameters.Scopes);
        set => SetKnownParameter(KnownParameters.Scopes, value);
    }

    /// <inheritdoc />
    public string? Username
    {
        get => GetKnownParameter(KnownParameters.Username);
        set => SetKnownParameter(KnownParameters.Username, value);
    }
}
