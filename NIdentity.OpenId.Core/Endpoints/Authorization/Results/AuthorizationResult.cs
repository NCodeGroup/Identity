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

using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization.Results;

internal class AuthorizationResult : OpenIdMessageResult, IAuthorizationResult
{
    public AuthorizationResult(Uri redirectUri, ResponseMode responseMode)
    {
        RedirectUri = redirectUri;
        ResponseMode = responseMode;
    }

    public Uri RedirectUri { get; set; }

    public ResponseMode ResponseMode { get; set; }

    public IOpenIdError? Error { get; set; }

    public string? State
    {
        get => GetKnownParameter(KnownParameters.State);
        set => SetKnownParameter(KnownParameters.State, value);
    }

    public string? Code
    {
        get => GetKnownParameter(KnownParameters.Code);
        set => SetKnownParameter(KnownParameters.Code, value);
    }

    public string? IdToken
    {
        get => GetKnownParameter(KnownParameters.IdToken);
        set => SetKnownParameter(KnownParameters.IdToken, value);
    }

    public string? AccessToken
    {
        get => GetKnownParameter(KnownParameters.AccessToken);
        set => SetKnownParameter(KnownParameters.AccessToken, value);
    }

    public string? TokenType
    {
        get => GetKnownParameter(KnownParameters.TokenType);
        set => SetKnownParameter(KnownParameters.TokenType, value);
    }

    public TimeSpan? ExpiresIn
    {
        get => GetKnownParameter(KnownParameters.ExpiresIn);
        set => SetKnownParameter(KnownParameters.ExpiresIn, value);
    }

    public string? Issuer
    {
        get => GetKnownParameter(KnownParameters.Issuer);
        set => SetKnownParameter(KnownParameters.Issuer, value);
    }

    public override async ValueTask ExecuteResultAsync(OpenIdEndpointContext context) =>
        await GetExecutor<IAuthorizationResult>(context).ExecuteResultAsync(context, this);
}
