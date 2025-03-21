﻿#region Copyright Preamble

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
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateAuthorizationRequestCommand"/> message.
/// </summary>
public class DefaultValidateAuthorizationRequestHandler : ICommandHandler<ValidateAuthorizationRequestCommand>, ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultOpenIdRegistration.MediatorPriorityHigh;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateAuthorizationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, authorizationRequest) = command;

        var openIdEnvironment = openIdContext.Environment;

        var requestMessage = authorizationRequest.OriginalRequestMessage;
        var requestObject = authorizationRequest.OriginalRequestObject;

        ValidateRequestMessage(requestMessage);

        if (requestObject != null)
            ValidateRequestObject(requestMessage, requestObject);

        ValidateRequest(openIdEnvironment, openIdClient, authorizationRequest);

        return ValueTask.CompletedTask;
    }

    [AssertionMethod]
    private void ValidateRequestMessage(IAuthorizationRequestMessage requestMessage)
    {
        var errorFactory = requestMessage.OpenIdEnvironment.ErrorFactory;

        var responseTypesCount = requestMessage.ResponseTypes?.Count ?? 0;
        if (responseTypesCount == 0)
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.ResponseType)
                .AsException();

        var redirectUri = requestMessage.RedirectUri;
        if (redirectUri is null)
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.RedirectUri)
                .AsException();
    }

    [AssertionMethod]
    private void ValidateRequestObject(
        IAuthorizationRequestMessage requestMessage,
        IAuthorizationRequestObject requestObject)
    {
        var errorFactory = requestMessage.OpenIdEnvironment.ErrorFactory;

        var errorCode = requestObject.RequestObjectSource == RequestObjectSource.Remote ?
            OpenIdConstants.ErrorCodes.InvalidRequestUri :
            OpenIdConstants.ErrorCodes.InvalidRequestJwt;

        /*
         * request and request_uri parameters MUST NOT be included in Request Objects.
         */

        if (requestObject.Parameters.Contains(OpenIdConstants.Parameters.Request))
            throw errorFactory
                .InvalidRequest("The JWT request object must not contain the 'request' parameter.", errorCode)
                .AsException();

        if (requestObject.Parameters.Contains(OpenIdConstants.Parameters.RequestUri))
            throw errorFactory
                .InvalidRequest("The JWT request object must not contain the 'request_uri' parameter.", errorCode)
                .AsException();

        /*
         * So that the request is a valid OAuth 2.0 Authorization Request, values for the response_type and client_id parameters MUST
         * be included using the OAuth 2.0 request syntax, since they are REQUIRED by OAuth 2.0. The values for these parameters MUST
         * match those in the Request Object, if present.
         */

        if (requestMessage.ResponseTypes != null && requestObject.ResponseTypes != null)
        {
            var requestMessageResponseTypes = requestMessage.ResponseTypes.Order();
            var requestObjectResponseTypes = requestObject.ResponseTypes.Order();
            if (!requestMessageResponseTypes.SequenceEqual(requestObjectResponseTypes))
                throw errorFactory
                    .InvalidRequest("The 'response_type' parameter in the JWT request object must match the same value from the request message.", errorCode)
                    .AsException();
        }

        /*
         * The Client ID values in the "client_id" request parameter and in the Request Object "client_id" claim MUST be identical.
         */

        if (string.IsNullOrEmpty(requestObject.ClientId))
            throw errorFactory
                .MissingParameter("The 'client_id' parameter in the JWT request object is missing.", errorCode)
                .AsException();

        if (!string.Equals(requestObject.ClientId, requestMessage.ClientId, StringComparison.Ordinal))
            throw errorFactory
                .InvalidRequest("The 'client_id' parameter in the JWT request object must match the same value from the request message.", errorCode)
                .AsException();
    }

    [AssertionMethod]
    private void ValidateRequest(
        OpenIdEnvironment openIdEnvironment,
        OpenIdClient openIdClient,
        IAuthorizationRequest request)
    {
        var errorFactory = openIdEnvironment.ErrorFactory;
        var settings = openIdClient.Settings;

        var requestedScopes = request.Scopes;
        var hasOpenIdScope = requestedScopes.Contains(OpenIdConstants.ScopeTypes.OpenId);

        var isImplicit = request.GrantType == OpenIdConstants.GrantTypes.Implicit;
        var isHybrid = request.GrantType == OpenIdConstants.GrantTypes.Hybrid;

        var hasCodeChallenge = !string.IsNullOrEmpty(request.CodeChallenge);
        var codeChallengeMethodIsPlain = !hasCodeChallenge || request.CodeChallengeMethod == OpenIdConstants.CodeChallengeMethods.Plain;

        if (requestedScopes.Count == 0)
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.Scope)
                .AsException();

        if (request.ResponseTypes.Count == 0)
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.ResponseType)
                .AsException();

        if (request.ResponseTypes.Contains(OpenIdConstants.ResponseTypes.IdToken) && string.IsNullOrEmpty(request.Nonce))
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.Nonce)
                .AsException();

        if (request.ResponseTypes.Contains(OpenIdConstants.ResponseTypes.IdToken) && !hasOpenIdScope)
            throw errorFactory
                .InvalidRequest("The 'openid' scope is required when requesting id tokens.")
                .AsException();

        if (request.ResponseMode == OpenIdConstants.ResponseModes.Query && request.GrantType != OpenIdConstants.GrantTypes.AuthorizationCode)
            throw errorFactory
                .InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.")
                .AsException();

        if (request.PromptTypes.Count > 1)
        {
            if (request.PromptTypes.Contains(OpenIdConstants.PromptTypes.None))
                throw errorFactory
                    .InvalidRequest("The 'none' prompt must not be combined with other values.")
                    .AsException();

            if (request.PromptTypes.Contains(OpenIdConstants.PromptTypes.CreateAccount))
                throw errorFactory
                    .InvalidRequest("The 'create' prompt must not be combined with other values.")
                    .AsException();
        }

        var hasNonce = !string.IsNullOrEmpty(request.Nonce);
        if (hasOpenIdScope && !hasNonce && (isImplicit || isHybrid))
            throw errorFactory
                .InvalidRequest("The nonce parameter is required when using the implicit or hybrid flows for openid requests.")
                .AsException();

        // perform configurable checks...

        if (request.OriginalRequestObject is null && settings.GetValue(SettingKeys.RequireRequestObject))
            throw errorFactory
                .InvalidRequest("The configuration requires the use of either request or request_uri parameters.")
                .AsException();

        // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
        if (request.ResponseTypes.Contains(OpenIdConstants.ResponseTypes.Token) && !settings.GetValue(SettingKeys.AllowUnsafeTokenResponse))
            throw errorFactory
                .UnauthorizedClient("The configuration prohibits the use of unsafe token responses.")
                .AsException();

        // require_pkce
        if (!hasCodeChallenge && settings.GetValue(SettingKeys.RequireCodeChallenge))
            throw errorFactory
                .UnauthorizedClient("The configuration requires the use of PKCE parameters.")
                .AsException();

        // allow_plain_code_challenge_method
        if (codeChallengeMethodIsPlain && !settings.GetValue(SettingKeys.AllowPlainCodeChallengeMethod))
            throw errorFactory
                .UnauthorizedClient("The configuration prohibits the plain PKCE method.")
                .AsException();

        // acr_values_supported
        if (settings.TryGetValue(SettingKeys.AcrValuesSupported, out var acrValuesSupported))
        {
            var acrValues = request.AcrValues;
            if (acrValues.Count > 0 && !acrValues.Except(acrValuesSupported).Any())
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.AcrValues)
                    .AsException();
        }

        // claims_locales_supported
        if (settings.TryGetValue(SettingKeys.ClaimsLocalesSupported, out var claimsLocalesSupported))
        {
            var claimsLocales = request.ClaimsLocales;
            if (claimsLocales.Count > 0 && !claimsLocales.Except(claimsLocalesSupported).Any())
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.ClaimsLocales)
                    .AsException();
        }

        // claims_parameter_supported
        if (settings.TryGetValue(SettingKeys.ClaimsParameterSupported, out var claimsParameterSupported))
        {
            var claimCount = request.Claims?.UserInfo?.Count ?? 0 + request.Claims?.IdToken?.Count ?? 0;
            if (claimCount > 0 && !claimsParameterSupported)
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.Claims)
                    .AsException();
        }

        // display_values_supported
        if (settings.TryGetValue(SettingKeys.DisplayValuesSupported, out var displayValuesSupported))
        {
            if (!displayValuesSupported.Contains(request.DisplayType))
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.Display)
                    .AsException();
        }

        // grant_types_supported
        if (settings.TryGetValue(SettingKeys.GrantTypesSupported, out var grantTypesSupported))
        {
            if (!grantTypesSupported.Contains(request.GrantType))
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.GrantType)
                    .AsException();
        }

        // prompt_values_supported
        if (settings.TryGetValue(SettingKeys.PromptValuesSupported, out var promptValuesSupported))
        {
            /*
             * https://openid.net/specs/openid-connect-prompt-create-1_0.html#section-4.1
             *
             * If the OpenID Provider receives a prompt value that it does not support (not declared in the
             * prompt_values_supported metadata field) the OP SHOULD respond with an HTTP 400 (Bad Request)
             * status code and an error value of invalid_request. It is RECOMMENDED that the OP return an
             * error_description value identifying the invalid parameter value.
             */
            var invalidPromptValues = request.PromptTypes.Except(promptValuesSupported).ToList();
            if (invalidPromptValues.Count != 0)
                throw errorFactory
                    .InvalidRequest($"The following prompt values are not supported: {string.Join(", ", invalidPromptValues)}")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
        }

        // response_modes_supported
        if (settings.TryGetValue(SettingKeys.ResponseModesSupported, out var responseModesSupported))
        {
            if (!responseModesSupported.Contains(request.ResponseMode))
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.ResponseMode)
                    .AsException();
        }

        // response_types_supported
        if (settings.TryGetValue(SettingKeys.ResponseTypesSupported, out var responseTypesSupported))
        {
            if (request.ResponseTypes.Except(responseTypesSupported).Any())
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.ResponseType)
                    .AsException();
        }

        // scopes_supported
        if (requestedScopes.Except(settings.GetValue(SettingKeys.ScopesSupported)).Any())
        {
            throw errorFactory.InvalidScope().AsException();
        }

        // TODO: other checks...

        // ui_locales_supported

        // Resource Indicators
        // https://datatracker.ietf.org/doc/html/rfc8707

        // check session cookie
    }
}
