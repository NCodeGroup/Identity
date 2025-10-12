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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Messages.Parameters;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

/// <summary>
/// Provides a base class for authorization request messages.
/// </summary>
/// <typeparam name="T">The type of the message.</typeparam>
[PublicAPI]
public abstract class BaseAuthorizationRequestMessage<T> : OpenIdMessage<T>
    where T : OpenIdMessage<T>, new()
{
    /// <inheritdoc />
    protected BaseAuthorizationRequestMessage()
    {
        // nothing
    }

    /// <inheritdoc />
    protected BaseAuthorizationRequestMessage(T other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    protected BaseAuthorizationRequestMessage(OpenIdEnvironment openIdEnvironment)
        : base(openIdEnvironment)
    {
        // nothing
    }

    /// <inheritdoc />
    protected BaseAuthorizationRequestMessage(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false)
        : base(openIdEnvironment, parameters, cloneParameters)
    {
        // nothing
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.AcrValues" />
    public List<string>? AcrValues
    {
        get => GetKnownParameter(OpenIdCommonParameters.AcrValues);
        set => SetKnownParameter(OpenIdCommonParameters.AcrValues, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Claims" />
    public IRequestClaims? Claims
    {
        get => GetKnownParameter(OpenIdAuthenticationParameters.Claims);
        set => SetKnownParameter(OpenIdAuthenticationParameters.Claims, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ClaimsLocales" />
    public List<string>? ClaimsLocales
    {
        get => GetKnownParameter(OpenIdCommonParameters.ClaimsLocales);
        set => SetKnownParameter(OpenIdCommonParameters.ClaimsLocales, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ClientId" />
    public string? ClientId
    {
        get => GetKnownParameter(OpenIdCommonParameters.ClientId);
        set => SetKnownParameter(OpenIdCommonParameters.ClientId, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeChallenge" />
    public string? CodeChallenge
    {
        get => GetKnownParameter(OpenIdCommonParameters.CodeChallenge);
        set => SetKnownParameter(OpenIdCommonParameters.CodeChallenge, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeChallengeMethod" />
    public string? CodeChallengeMethod
    {
        get => GetKnownParameter(OpenIdCommonParameters.CodeChallengeMethod);
        set => SetKnownParameter(OpenIdCommonParameters.CodeChallengeMethod, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeVerifier" />
    public string? CodeVerifier
    {
        get => GetKnownParameter(OpenIdCommonParameters.CodeVerifier);
        set => SetKnownParameter(OpenIdCommonParameters.CodeVerifier, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.DisplayType" />
    public string? DisplayType
    {
        get => GetKnownParameter(OpenIdCommonParameters.DisplayType);
        set => SetKnownParameter(OpenIdCommonParameters.DisplayType, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.IdTokenHint" />
    public string? IdTokenHint
    {
        get => GetKnownParameter(OpenIdCommonParameters.IdTokenHint);
        set => SetKnownParameter(OpenIdCommonParameters.IdTokenHint, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.LoginHint" />
    public string? LoginHint
    {
        get => GetKnownParameter(OpenIdCommonParameters.LoginHint);
        set => SetKnownParameter(OpenIdCommonParameters.LoginHint, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.MaxAge" />
    public TimeSpan? MaxAge
    {
        get => GetKnownParameter(OpenIdCommonParameters.MaxAge);
        set => SetKnownParameter(OpenIdCommonParameters.MaxAge, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Nonce" />
    public string? Nonce
    {
        get => GetKnownParameter(OpenIdCommonParameters.Nonce);
        set => SetKnownParameter(OpenIdCommonParameters.Nonce, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.PromptTypes" />
    public List<string>? PromptTypes
    {
        get => GetKnownParameter(OpenIdCommonParameters.PromptType);
        set => SetKnownParameter(OpenIdCommonParameters.PromptType, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RedirectUri" />
    public Uri? RedirectUri
    {
        get => GetKnownParameter(OpenIdCommonParameters.RedirectUri);
        set => SetKnownParameter(OpenIdCommonParameters.RedirectUri, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RequestJwt" />
    public string? RequestJwt
    {
        get => GetKnownParameter(OpenIdCommonParameters.RequestJwt);
        set => SetKnownParameter(OpenIdCommonParameters.RequestJwt, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RequestUri" />
    public Uri? RequestUri
    {
        get => GetKnownParameter(OpenIdCommonParameters.RequestUri);
        set => SetKnownParameter(OpenIdCommonParameters.RequestUri, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ResponseMode" />
    public string? ResponseMode
    {
        get => GetKnownParameter(OpenIdCommonParameters.ResponseMode);
        set => SetKnownParameter(OpenIdCommonParameters.ResponseMode, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ResponseTypes" />
    public List<string>? ResponseTypes
    {
        get => GetKnownParameter(OpenIdCommonParameters.ResponseTypes);
        set => SetKnownParameter(OpenIdCommonParameters.ResponseTypes, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Scopes" />
    public List<string>? Scopes
    {
        get => GetKnownParameter(OpenIdCommonParameters.Scopes);
        set => SetKnownParameter(OpenIdCommonParameters.Scopes, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.State" />
    public string? State
    {
        get => GetKnownParameter(OpenIdCommonParameters.State);
        set => SetKnownParameter(OpenIdCommonParameters.State, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.UiLocales" />
    public List<string>? UiLocales
    {
        get => GetKnownParameter(OpenIdCommonParameters.UiLocales);
        set => SetKnownParameter(OpenIdCommonParameters.UiLocales, value);
    }
}
