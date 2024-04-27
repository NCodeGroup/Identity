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

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

internal abstract class BaseAuthorizationRequestMessage<T, TProperties> : OpenIdMessage<T, TProperties>
    where T : OpenIdMessage<T, TProperties>, new()
    where TProperties : class, new()
{
    public IReadOnlyCollection<string>? AcrValues
    {
        get => GetKnownParameter(KnownParameters.AcrValues);
        set => SetKnownParameter(KnownParameters.AcrValues, value);
    }

    public IRequestClaims? Claims
    {
        get => GetKnownParameter(KnownParameters.Claims);
        set => SetKnownParameter(KnownParameters.Claims, value);
    }

    public IReadOnlyCollection<string>? ClaimsLocales
    {
        get => GetKnownParameter(KnownParameters.ClaimsLocales);
        set => SetKnownParameter(KnownParameters.ClaimsLocales, value);
    }

    public string? ClientId
    {
        get => GetKnownParameter(KnownParameters.ClientId);
        set => SetKnownParameter(KnownParameters.ClientId, value);
    }

    public string? CodeChallenge
    {
        get => GetKnownParameter(KnownParameters.CodeChallenge);
        set => SetKnownParameter(KnownParameters.CodeChallenge, value);
    }

    public CodeChallengeMethod? CodeChallengeMethod
    {
        get => GetKnownParameter(KnownParameters.CodeChallengeMethod);
        set => SetKnownParameter(KnownParameters.CodeChallengeMethod, value);
    }

    public string? CodeVerifier
    {
        get => GetKnownParameter(KnownParameters.CodeVerifier);
        set => SetKnownParameter(KnownParameters.CodeVerifier, value);
    }

    public DisplayType? DisplayType
    {
        get => GetKnownParameter(KnownParameters.DisplayType);
        set => SetKnownParameter(KnownParameters.DisplayType, value);
    }

    public string? IdTokenHint
    {
        get => GetKnownParameter(KnownParameters.IdTokenHint);
        set => SetKnownParameter(KnownParameters.IdTokenHint, value);
    }

    public string? LoginHint
    {
        get => GetKnownParameter(KnownParameters.LoginHint);
        set => SetKnownParameter(KnownParameters.LoginHint, value);
    }

    public TimeSpan? MaxAge
    {
        get => GetKnownParameter(KnownParameters.MaxAge);
        set => SetKnownParameter(KnownParameters.MaxAge, value);
    }

    public string? Nonce
    {
        get => GetKnownParameter(KnownParameters.Nonce);
        set => SetKnownParameter(KnownParameters.Nonce, value);
    }

    public PromptTypes? PromptType
    {
        get => GetKnownParameter(KnownParameters.PromptType);
        set => SetKnownParameter(KnownParameters.PromptType, value);
    }

    public Uri? RedirectUri
    {
        get => GetKnownParameter(KnownParameters.RedirectUri);
        set => SetKnownParameter(KnownParameters.RedirectUri, value);
    }

    public string? RequestJwt
    {
        get => GetKnownParameter(KnownParameters.RequestJwt);
        set => SetKnownParameter(KnownParameters.RequestJwt, value);
    }

    public Uri? RequestUri
    {
        get => GetKnownParameter(KnownParameters.RequestUri);
        set => SetKnownParameter(KnownParameters.RequestUri, value);
    }

    public ResponseMode? ResponseMode
    {
        get => GetKnownParameter(KnownParameters.ResponseMode);
        set => SetKnownParameter(KnownParameters.ResponseMode, value);
    }

    public ResponseTypes? ResponseType
    {
        get => GetKnownParameter(KnownParameters.ResponseType);
        set => SetKnownParameter(KnownParameters.ResponseType, value);
    }

    public IReadOnlyCollection<string>? Scopes
    {
        get => GetKnownParameter(KnownParameters.Scopes);
        set => SetKnownParameter(KnownParameters.Scopes, value);
    }

    public string? State
    {
        get => GetKnownParameter(KnownParameters.State);
        set => SetKnownParameter(KnownParameters.State, value);
    }

    public IReadOnlyCollection<string>? UiLocales
    {
        get => GetKnownParameter(KnownParameters.UiLocales);
        set => SetKnownParameter(KnownParameters.UiLocales, value);
    }
}
