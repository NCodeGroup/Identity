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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

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
        get => GetKnownParameter(KnownParameters.AcrValues);
        set => SetKnownParameter(KnownParameters.AcrValues, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Claims" />
    public IRequestClaims? Claims
    {
        get => GetKnownParameter(KnownParameters.Claims);
        set => SetKnownParameter(KnownParameters.Claims, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ClaimsLocales" />
    public List<string>? ClaimsLocales
    {
        get => GetKnownParameter(KnownParameters.ClaimsLocales);
        set => SetKnownParameter(KnownParameters.ClaimsLocales, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ClientId" />
    public string? ClientId
    {
        get => GetKnownParameter(KnownParameters.ClientId);
        set => SetKnownParameter(KnownParameters.ClientId, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeChallenge" />
    public string? CodeChallenge
    {
        get => GetKnownParameter(KnownParameters.CodeChallenge);
        set => SetKnownParameter(KnownParameters.CodeChallenge, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeChallengeMethod" />
    public string? CodeChallengeMethod
    {
        get => GetKnownParameter(KnownParameters.CodeChallengeMethod);
        set => SetKnownParameter(KnownParameters.CodeChallengeMethod, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.CodeVerifier" />
    public string? CodeVerifier
    {
        get => GetKnownParameter(KnownParameters.CodeVerifier);
        set => SetKnownParameter(KnownParameters.CodeVerifier, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.DisplayType" />
    public string? DisplayType
    {
        get => GetKnownParameter(KnownParameters.DisplayType);
        set => SetKnownParameter(KnownParameters.DisplayType, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.IdTokenHint" />
    public string? IdTokenHint
    {
        get => GetKnownParameter(KnownParameters.IdTokenHint);
        set => SetKnownParameter(KnownParameters.IdTokenHint, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.LoginHint" />
    public string? LoginHint
    {
        get => GetKnownParameter(KnownParameters.LoginHint);
        set => SetKnownParameter(KnownParameters.LoginHint, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.MaxAge" />
    public TimeSpan? MaxAge
    {
        get => GetKnownParameter(KnownParameters.MaxAge);
        set => SetKnownParameter(KnownParameters.MaxAge, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Nonce" />
    public string? Nonce
    {
        get => GetKnownParameter(KnownParameters.Nonce);
        set => SetKnownParameter(KnownParameters.Nonce, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.PromptTypes" />
    public List<string>? PromptTypes
    {
        get => GetKnownParameter(KnownParameters.PromptType);
        set => SetKnownParameter(KnownParameters.PromptType, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RedirectUri" />
    public Uri? RedirectUri
    {
        get => GetKnownParameter(KnownParameters.RedirectUri);
        set => SetKnownParameter(KnownParameters.RedirectUri, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RequestJwt" />
    public string? RequestJwt
    {
        get => GetKnownParameter(KnownParameters.RequestJwt);
        set => SetKnownParameter(KnownParameters.RequestJwt, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.RequestUri" />
    public Uri? RequestUri
    {
        get => GetKnownParameter(KnownParameters.RequestUri);
        set => SetKnownParameter(KnownParameters.RequestUri, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ResponseMode" />
    public string? ResponseMode
    {
        get => GetKnownParameter(KnownParameters.ResponseMode);
        set => SetKnownParameter(KnownParameters.ResponseMode, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.ResponseTypes" />
    public List<string>? ResponseTypes
    {
        get => GetKnownParameter(KnownParameters.ResponseTypes);
        set => SetKnownParameter(KnownParameters.ResponseTypes, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.Scopes" />
    public List<string>? Scopes
    {
        get => GetKnownParameter(KnownParameters.Scopes);
        set => SetKnownParameter(KnownParameters.Scopes, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.State" />
    public string? State
    {
        get => GetKnownParameter(KnownParameters.State);
        set => SetKnownParameter(KnownParameters.State, value);
    }

    /// <inheritdoc cref="IAuthorizationRequestMessage.UiLocales" />
    public List<string>? UiLocales
    {
        get => GetKnownParameter(KnownParameters.UiLocales);
        set => SetKnownParameter(KnownParameters.UiLocales, value);
    }
}
