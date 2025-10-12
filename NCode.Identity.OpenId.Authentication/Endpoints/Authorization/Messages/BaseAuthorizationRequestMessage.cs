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
        get => GetKnownParameter(CoreParameters.AcrValues);
        set => SetKnownParameter(CoreParameters.AcrValues, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Claims" />
    public IRequestClaims? Claims
    {
        get => GetKnownParameter(AuthParameters.Claims);
        set => SetKnownParameter(AuthParameters.Claims, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ClaimsLocales" />
    public List<string>? ClaimsLocales
    {
        get => GetKnownParameter(CoreParameters.ClaimsLocales);
        set => SetKnownParameter(CoreParameters.ClaimsLocales, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ClientId" />
    public string? ClientId
    {
        get => GetKnownParameter(CoreParameters.ClientId);
        set => SetKnownParameter(CoreParameters.ClientId, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeChallenge" />
    public string? CodeChallenge
    {
        get => GetKnownParameter(CoreParameters.CodeChallenge);
        set => SetKnownParameter(CoreParameters.CodeChallenge, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeChallengeMethod" />
    public string? CodeChallengeMethod
    {
        get => GetKnownParameter(CoreParameters.CodeChallengeMethod);
        set => SetKnownParameter(CoreParameters.CodeChallengeMethod, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeVerifier" />
    public string? CodeVerifier
    {
        get => GetKnownParameter(CoreParameters.CodeVerifier);
        set => SetKnownParameter(CoreParameters.CodeVerifier, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.DisplayType" />
    public string? DisplayType
    {
        get => GetKnownParameter(CoreParameters.DisplayType);
        set => SetKnownParameter(CoreParameters.DisplayType, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.IdTokenHint" />
    public string? IdTokenHint
    {
        get => GetKnownParameter(CoreParameters.IdTokenHint);
        set => SetKnownParameter(CoreParameters.IdTokenHint, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.LoginHint" />
    public string? LoginHint
    {
        get => GetKnownParameter(CoreParameters.LoginHint);
        set => SetKnownParameter(CoreParameters.LoginHint, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.MaxAge" />
    public TimeSpan? MaxAge
    {
        get => GetKnownParameter(CoreParameters.MaxAge);
        set => SetKnownParameter(CoreParameters.MaxAge, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Nonce" />
    public string? Nonce
    {
        get => GetKnownParameter(CoreParameters.Nonce);
        set => SetKnownParameter(CoreParameters.Nonce, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.PromptTypes" />
    public List<string>? PromptTypes
    {
        get => GetKnownParameter(CoreParameters.PromptType);
        set => SetKnownParameter(CoreParameters.PromptType, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RedirectUri" />
    public Uri? RedirectUri
    {
        get => GetKnownParameter(CoreParameters.RedirectUri);
        set => SetKnownParameter(CoreParameters.RedirectUri, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RequestJwt" />
    public string? RequestJwt
    {
        get => GetKnownParameter(CoreParameters.RequestJwt);
        set => SetKnownParameter(CoreParameters.RequestJwt, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RequestUri" />
    public Uri? RequestUri
    {
        get => GetKnownParameter(CoreParameters.RequestUri);
        set => SetKnownParameter(CoreParameters.RequestUri, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ResponseMode" />
    public string? ResponseMode
    {
        get => GetKnownParameter(CoreParameters.ResponseMode);
        set => SetKnownParameter(CoreParameters.ResponseMode, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ResponseTypes" />
    public List<string>? ResponseTypes
    {
        get => GetKnownParameter(CoreParameters.ResponseTypes);
        set => SetKnownParameter(CoreParameters.ResponseTypes, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Scopes" />
    public List<string>? Scopes
    {
        get => GetKnownParameter(CoreParameters.Scopes);
        set => SetKnownParameter(CoreParameters.Scopes, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.State" />
    public string? State
    {
        get => GetKnownParameter(CoreParameters.State);
        set => SetKnownParameter(CoreParameters.State, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.UiLocales" />
    public List<string>? UiLocales
    {
        get => GetKnownParameter(CoreParameters.UiLocales);
        set => SetKnownParameter(CoreParameters.UiLocales, value);
    }
}
