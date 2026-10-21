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

using System.Collections.Frozen;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Encoders;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Pkce;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateAuthorizationRequestCommand"/> message.
/// </summary>
internal class DefaultValidateAuthorizationRequestHandler(
    IClientScopeService clientScopeService,
    ISecurityProfileCatalog securityProfileCatalog,
    IEnumerable<IAuthorizationResponseModeHandler> responseModeHandlers
) : ICommandHandler<ValidateAuthorizationRequestCommand>, ISupportMediatorPriority
{
    private FrozenSet<string> ServedResponseModes { get; } =
        responseModeHandlers
            .Select(handler => handler.ResponseMode)
            .ToFrozenSet(StringComparer.Ordinal);

    private IClientScopeService ClientScopeService { get; } = clientScopeService;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateAuthorizationRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, authorizationRequest) = command;

        var openIdEnvironment = openIdContext.Environment;

        var requestMessage = authorizationRequest.OriginalRequestMessage;
        var requestObject = authorizationRequest.OriginalRequestObject;

        ValidateRequestMessage(requestMessage);

        if (requestObject != null)
            ValidateRequestObject(requestMessage, requestObject);

        ValidateRequest(openIdContext, openIdClient, authorizationRequest);

        // Apply any security-profile authorization-request requirements (e.g. FAPI's confidential-client rule).
        await SecurityProfileCatalog.EvaluateRequirementsAsync(
            openIdClient.Settings,
            new AuthorizationRequestProfileContext(
                openIdContext,
                openIdClient,
                authorizationRequest
            ),
            cancellationToken
        );

        // A requested scope must be permitted by the client's grants (ADR-0026).
        var allowedScopes = await ClientScopeService.GetAllowedScopesAsync(
            openIdContext.Tenant.TenantId,
            openIdClient.ClientId,
            cancellationToken
        );
        if (authorizationRequest.Scopes.Except(allowedScopes).Any())
            throw openIdEnvironment.ErrorFactory.InvalidScope().AsException();

        // RFC 8707: each requested resource indicator must be a known, enabled resource server (audience).
        if (authorizationRequest.Resources.Count > 0)
        {
            var enabledResourceServers =
                await ClientScopeService.GetEnabledResourceServerIdentifiersAsync(
                    openIdContext.Tenant.TenantId,
                    cancellationToken
                );
            if (authorizationRequest.Resources.Except(enabledResourceServers).Any())
                throw openIdEnvironment
                    .ErrorFactory.InvalidTarget(
                        "One or more requested resource indicators are unknown or disabled."
                    )
                    .AsException();
        }
    }

    [AssertionMethod]
    private static void ValidateRequestMessage(IAuthorizationRequestMessage requestMessage)
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
    private static void ValidateRequestObject(
        IAuthorizationRequestMessage requestMessage,
        IAuthorizationRequestObject requestObject
    )
    {
        var errorFactory = requestMessage.OpenIdEnvironment.ErrorFactory;

        var errorCode =
            requestObject.RequestObjectSource == RequestObjectSource.Remote
                ? OpenIdConstants.ErrorCodes.InvalidRequestUri
                : OpenIdConstants.ErrorCodes.InvalidRequestJwt;

        /*
         * request and request_uri parameters MUST NOT be included in Request Objects.
         */

        if (requestObject.Parameters.Contains(OpenIdConstants.Parameters.Request))
            throw errorFactory
                .InvalidRequest(
                    "The JWT request object must not contain the 'request' parameter.",
                    errorCode
                )
                .AsException();

        if (requestObject.Parameters.Contains(OpenIdConstants.Parameters.RequestUri))
            throw errorFactory
                .InvalidRequest(
                    "The JWT request object must not contain the 'request_uri' parameter.",
                    errorCode
                )
                .AsException();

        /*
         * So that the request is a valid OAuth 2.0 Authorization Request, values for the response_type and client_id parameters MUST
         * be included using the OAuth 2.0 request syntax, since they are REQUIRED by OAuth 2.0. The values for these parameters MUST
         * match those in the Request Object, if present.
         */

        if (requestMessage.ResponseTypes != null && requestObject.ResponseTypes != null)
        {
            var requestMessageResponseType = ResponseTypeValues.Canonicalize(
                requestMessage.ResponseTypes
            );
            var requestObjectResponseType = ResponseTypeValues.Canonicalize(
                requestObject.ResponseTypes
            );
            if (
                !string.Equals(
                    requestMessageResponseType,
                    requestObjectResponseType,
                    StringComparison.Ordinal
                )
            )
                throw errorFactory
                    .InvalidRequest(
                        "The 'response_type' parameter in the JWT request object must match the same value from the request message.",
                        errorCode
                    )
                    .AsException();
        }

        /*
         * The Client ID values in the "client_id" request parameter and in the Request Object "client_id" claim MUST be identical.
         */

        if (string.IsNullOrEmpty(requestObject.ClientId))
            throw errorFactory
                .MissingParameter(
                    "The 'client_id' parameter in the JWT request object is missing.",
                    errorCode
                )
                .AsException();

        if (
            !string.Equals(
                requestObject.ClientId,
                requestMessage.ClientId,
                StringComparison.Ordinal
            )
        )
            throw errorFactory
                .InvalidRequest(
                    "The 'client_id' parameter in the JWT request object must match the same value from the request message.",
                    errorCode
                )
                .AsException();
    }

    [AssertionMethod]
    private void ValidateRequest(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest request
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        ValidateProtocolRules(errorFactory, request);
        ValidateConfiguredConstraints(errorFactory, settings, request);
        ValidateResponseType(openIdContext, openIdClient, request);
        ValidateCodeChallenge(errorFactory, openIdClient, request);

        // A response mode is served only by a registered handler (ADR-0201).
        if (!ServedResponseModes.Contains(request.ResponseMode))
            throw errorFactory.NotSupported(OpenIdConstants.Parameters.ResponseMode).AsException();

        ValidateSupportedValues(errorFactory, settings, request);
    }

    [AssertionMethod]
    private static void ValidateProtocolRules(
        IOpenIdErrorFactory errorFactory,
        IAuthorizationRequest request
    )
    {
        var requestedScopes = request.Scopes;
        var hasOpenIdScope = requestedScopes.Contains(OpenIdConstants.ScopeTypes.OpenId);

        // A response type that includes 'id_token' or 'token' returns a token from the authorization endpoint (the
        // implicit and hybrid flows).
        var returnsTokens = ResponseTypeValues.ReturnsTokens(request.ResponseTypes);

        if (requestedScopes.Count == 0)
            throw errorFactory.MissingParameter(OpenIdConstants.Parameters.Scope).AsException();

        if (request.ResponseTypes.Count == 0)
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.ResponseType)
                .AsException();

        if (
            request.ResponseTypes.Contains(OpenIdConstants.ResponseTypes.IdToken)
            && string.IsNullOrEmpty(request.Nonce)
        )
            throw errorFactory.MissingParameter(OpenIdConstants.Parameters.Nonce).AsException();

        if (
            request.ResponseTypes.Contains(OpenIdConstants.ResponseTypes.IdToken) && !hasOpenIdScope
        )
            throw errorFactory
                .InvalidRequest("The 'openid' scope is required when requesting id tokens.")
                .AsException();

        // A token never travels in a query string (OAuth 2.0 Multiple Response Type Encoding Practices §2.1), not even
        // inside a JWT-secured authorization response (JARM §2.3.1).
        if (
            returnsTokens
            && request.ResponseMode
                is OpenIdConstants.ResponseModes.Query
                    or OpenIdConstants.ResponseModes.QueryJwt
        )
            throw errorFactory
                .InvalidRequest(
                    $"The '{request.ResponseMode}' response mode must not be used with a response type that returns a token or an ID token."
                )
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
        if (hasOpenIdScope && !hasNonce && returnsTokens)
            throw errorFactory
                .InvalidRequest(
                    "The nonce parameter is required when using the implicit or hybrid flows for openid requests."
                )
                .AsException();
    }

    [AssertionMethod]
    internal static void ValidateConfiguredConstraints(
        IOpenIdErrorFactory errorFactory,
        IReadOnlySettingCollection settings,
        IAuthorizationRequest request
    )
    {
        if (
            request.OriginalRequestObject is null
            && settings.GetValue(OpenIdSettingKeys.RequireRequestObject)
        )
            throw errorFactory
                .InvalidRequest(
                    "The configuration requires the use of either request or request_uri parameters."
                )
                .AsException();

        // dpop_jkt (RFC 9449 §10): a base64url-encoded SHA-256 JWK thumbprint (RFC 7638), i.e. exactly 32 bytes.
        var dpopJkt = request.DPoPJwkThumbprint;
        if (!string.IsNullOrEmpty(dpopJkt))
        {
            Span<byte> buffer = stackalloc byte[Base64Url.GetByteCountForDecode(dpopJkt.Length)];
            if (!Base64Url.TryDecode(dpopJkt, buffer, out var bytesWritten) || bytesWritten != 32)
                throw errorFactory
                    .InvalidRequest(
                        "The 'dpop_jkt' parameter is not a valid base64url-encoded SHA-256 thumbprint."
                    )
                    .AsException();
        }
    }

    /// <summary>
    /// Validates the requested response type. A response type is supported where it is listed, as a whole value, in
    /// <c>response_types_supported</c> and every grant type it uses is listed in <c>grant_types_supported</c> (OpenID
    /// Connect Dynamic Client Registration 1.0 §2); an unset list is unrestricted. A response type the tenant does not
    /// support is rejected with <c>unsupported_response_type</c>, and one the tenant supports but the client may not
    /// use with <c>unauthorized_client</c> (RFC 6749 §4.1.2.1). This is the only control over the tokens the
    /// authorization endpoint returns: a tenant or client forbids an access token there by listing no response type
    /// that includes <c>token</c>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> whose tenant's settings define what the server
    /// supports.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> whose settings define what the client may use.</param>
    /// <param name="request">The authorization request to validate.</param>
    // See ADR-0141 and ADR-0142.
    [AssertionMethod]
    internal static void ValidateResponseType(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest request
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var tenantSettings = openIdContext.Tenant.SettingsProvider.Collection;

        var responseTypes = request.ResponseTypes;
        var responseType = ResponseTypeValues.Canonicalize(responseTypes);
        var grantTypes = ResponseTypeValues.GetGrantTypes(responseTypes);

        if (!SupportsResponseType(tenantSettings, responseType, grantTypes))
            throw errorFactory
                .UnsupportedResponseType($"The response type '{responseType}' is not supported.")
                .AsException();

        if (!SupportsResponseType(openIdClient.Settings, responseType, grantTypes))
            throw errorFactory
                .UnauthorizedClient(
                    $"The client is not authorized to use the response type '{responseType}'."
                )
                .AsException();
    }

    private static bool SupportsResponseType(
        IReadOnlySettingCollection settings,
        string responseType,
        IReadOnlyList<string> grantTypes
    )
    {
        if (
            settings.TryGetValue(
                OpenIdSettingKeys.ResponseTypesSupported,
                out var responseTypesSupported
            ) && !responseTypesSupported.Contains(responseType, StringComparer.Ordinal)
        )
            return false;

        return !settings.TryGetValue(
                OpenIdSettingKeys.GrantTypesSupported,
                out var grantTypesSupported
            )
            || grantTypes.All(grantType =>
                grantTypesSupported.Contains(grantType, StringComparer.Ordinal)
            );
    }

    /// <summary>
    /// Validates the PKCE parameters (RFC 7636). A request whose response type issues an authorization code carries a
    /// <c>code_challenge</c> unless <see cref="CodeChallengeRequirement"/> exempts it. A <c>code_challenge</c> has the
    /// RFC 7636 syntax and names its <c>code_challenge_method</c>, which has no default, and the method must be one of
    /// the client's <c>code_challenge_methods_supported</c>. Each refusal is <c>invalid_request</c>
    /// (RFC 7636 §4.4.1).
    /// </summary>
    /// <param name="errorFactory">The <see cref="IOpenIdErrorFactory"/> that creates the refusal.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> whose registration and settings decide the
    /// requirement.</param>
    /// <param name="request">The authorization request to validate.</param>
    // See ADR-0144.
    [AssertionMethod]
    internal static void ValidateCodeChallenge(
        IOpenIdErrorFactory errorFactory,
        OpenIdClient openIdClient,
        IAuthorizationRequest request
    )
    {
        var codeChallenge = request.CodeChallenge;
        if (string.IsNullOrEmpty(codeChallenge))
        {
            if (
                ResponseTypeValues.IssuesAuthorizationCode(request.ResponseTypes)
                && CodeChallengeRequirement.IsRequiredForAuthorization(openIdClient, request)
            )
                throw errorFactory
                    .MissingParameter(OpenIdConstants.Parameters.CodeChallenge)
                    .AsException();

            return;
        }

        if (!PkceValueSyntax.IsWellFormed(codeChallenge))
            throw errorFactory
                .InvalidParameterValue(OpenIdConstants.Parameters.CodeChallenge)
                .AsException();

        var codeChallengeMethod = request.CodeChallengeMethod;
        if (string.IsNullOrEmpty(codeChallengeMethod))
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.CodeChallengeMethod)
                .AsException();

        var codeChallengeMethodsSupported = openIdClient.Settings.GetValue(
            OpenIdSettingKeys.CodeChallengeMethodsSupported
        );
        if (!codeChallengeMethodsSupported.Contains(codeChallengeMethod, StringComparer.Ordinal))
            throw errorFactory
                .NotSupported(OpenIdConstants.Parameters.CodeChallengeMethod)
                .AsException();
    }

    [AssertionMethod]
    internal static void ValidateSupportedValues(
        IOpenIdErrorFactory errorFactory,
        IReadOnlySettingCollection settings,
        IAuthorizationRequest request
    )
    {
        // acr_values_supported
        if (settings.TryGetValue(OpenIdSettingKeys.AcrValuesSupported, out var acrValuesSupported))
        {
            var acrValues = request.AcrValues;
            if (acrValues.Count > 0 && acrValues.Except(acrValuesSupported).Any())
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.AcrValues).AsException();
        }

        // code_challenge_methods_supported is enforced by ValidateCodeChallenge

        // claims_locales_supported
        if (
            settings.TryGetValue(
                OpenIdSettingKeys.ClaimsLocalesSupported,
                out var claimsLocalesSupported
            )
        )
        {
            var claimsLocales = request.ClaimsLocales;
            if (claimsLocales.Count > 0 && claimsLocales.Except(claimsLocalesSupported).Any())
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.ClaimsLocales)
                    .AsException();
        }

        // claims_parameter_supported
        if (
            settings.TryGetValue(
                OpenIdSettingKeys.ClaimsParameterSupported,
                out var claimsParameterSupported
            )
        )
        {
            var claimCount =
                (request.Claims?.UserInfo?.Count ?? 0) + (request.Claims?.IdToken?.Count ?? 0);
            if (claimCount > 0 && !claimsParameterSupported)
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.Claims).AsException();
        }

        // display_values_supported
        if (
            settings.TryGetValue(
                OpenIdSettingKeys.DisplayValuesSupported,
                out var displayValuesSupported
            )
        )
        {
            if (!displayValuesSupported.Contains(request.DisplayType))
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.Display).AsException();
        }

        // grant_types_supported and response_types_supported are enforced by ValidateResponseType

        // prompt_values_supported
        if (
            settings.TryGetValue(
                OpenIdSettingKeys.PromptValuesSupported,
                out var promptValuesSupported
            )
        )
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
                    .InvalidRequest(
                        $"The following prompt values are not supported: {string.Join(", ", invalidPromptValues)}"
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
        }

        // response_modes_supported
        if (
            settings.TryGetValue(
                OpenIdSettingKeys.ResponseModesSupported,
                out var responseModesSupported
            )
        )
        {
            if (!responseModesSupported.Contains(request.ResponseMode))
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.ResponseMode)
                    .AsException();
        }

        // scopes_supported is enforced in HandleAsync via IClientScopeService (ADR-0026)

        // TODO: other checks...

        // ui_locales_supported

        // Resource Indicators
        // https://datatracker.ietf.org/doc/html/rfc8707

        // check session cookie
    }
}
