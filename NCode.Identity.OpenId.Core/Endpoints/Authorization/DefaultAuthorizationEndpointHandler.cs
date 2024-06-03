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

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Extensions;
using NCode.Identity.OpenId.Logic.Authorization;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the authorization endpoint.
/// </summary>
public class DefaultAuthorizationEndpointHandler(
    ILogger<DefaultAuthorizationEndpointHandler> logger,
    TimeProvider timeProvider,
    IHttpClientFactory httpClientFactory,
    IJsonWebTokenService jsonWebTokenService,
    IClientAuthenticationService clientAuthenticationService,
    IAuthenticationSchemeProvider authenticationSchemeProvider,
    IAuthorizationInteractionService interactionService,
    IAuthorizationCodeService authorizationCodeService,
    IOpenIdContextFactory contextFactory,
    IContinueService continueService,
    ITokenService tokenService,
    OpenIdServer openIdServer
) :
    IOpenIdEndpointProvider,
    IContinueProvider,
    ICommandResponseHandler<LoadAuthorizationSourceCommand, IAuthorizationSource>,
    ICommandResponseHandler<LoadAuthorizationRequestCommand, IAuthorizationRequest>,
    ICommandHandler<ValidateAuthorizationRequestCommand>,
    ICommandResponseHandler<AuthenticateCommand, AuthenticateSubjectResult>,
    ICommandResponseHandler<AuthorizeCommand, IResult?>,
    ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private bool DefaultSignInSchemeFetched { get; set; }
    private string? DefaultSignInSchemeName { get; set; }

    private ILogger<DefaultAuthorizationEndpointHandler> Logger { get; } = logger;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private IJsonWebTokenService JsonWebTokenService { get; } = jsonWebTokenService;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;
    private IAuthenticationSchemeProvider AuthenticationSchemeProvider { get; } = authenticationSchemeProvider;
    private IAuthorizationInteractionService InteractionService { get; } = interactionService;
    private IAuthorizationCodeService AuthorizationCodeService { get; } = authorizationCodeService;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IContinueService ContinueService { get; } = continueService;
    private ITokenService TokenService { get; } = tokenService;
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdErrorFactory ErrorFactory => OpenIdServer.ErrorFactory;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapMethods(
            OpenIdConstants.EndpointPaths.Authorization,
            new[] { HttpMethods.Get, HttpMethods.Post },
            HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Authorization)
        .WithOpenIdDiscoverable();

    private async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var openIdContext = await ContextFactory.CreateAsync(
            httpContext,
            mediator,
            cancellationToken);

        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken);

        // for errors, before we redirect, we must validate the client_id and redirect_uri
        // otherwise we must return a failure HTTP status code

        if (authResult.IsError)
        {
            return authResult.Error.AsResult();
        }

        if (!authResult.HasClient)
        {
            return ErrorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsResult();
        }

        // the following tries its best to not throw for OpenID protocol errors
        var authorizationSource = await mediator.SendAsync<LoadAuthorizationSourceCommand, IAuthorizationSource>(
            new LoadAuthorizationSourceCommand(openIdContext),
            cancellationToken);

        // this will throw if client_id or redirect_uri are invalid
        var openIdClient = authResult.Client;
        var clientRedirectContext = GetClientRedirectContext(
            openIdClient,
            authorizationSource);

        // everything after this point is safe to redirect to the client

        var redirectUri = clientRedirectContext.RedirectUri;
        var responseMode = clientRedirectContext.ResponseMode;

        try
        {
            var authorizationRequest = await mediator.SendAsync<LoadAuthorizationRequestCommand, IAuthorizationRequest>(
                new LoadAuthorizationRequestCommand(
                    openIdContext,
                    openIdClient,
                    authorizationSource),
                cancellationToken);

            return await ProcessAuthorizationRequestAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                clientRedirectContext,
                cancellationToken);
        }
        catch (Exception exception)
        {
            var openIdError = exception is OpenIdException openIdException ?
                openIdException.Error :
                ErrorFactory
                    .Create(OpenIdConstants.ErrorCodes.ServerError)
                    .WithException(exception);

            openIdError.WithState(clientRedirectContext.State);

            return new AuthorizationResult(redirectUri, responseMode, openIdError);
        }
    }

    private ClientRedirectContext GetClientRedirectContext(OpenIdClient openIdClient, IOpenIdMessage authorizationSource)
    {
        var hasState = authorizationSource.TryGetValue(OpenIdConstants.Parameters.State, out var stateStringValues);
        var state = hasState && !StringValues.IsNullOrEmpty(stateStringValues) ? stateStringValues.ToString() : null;

        var hasResponseMode = !authorizationSource.TryGetValue(OpenIdConstants.Parameters.ResponseMode, out var responseModeStringValues);
        if (!hasResponseMode || !Enum.TryParse(responseModeStringValues, out ResponseMode responseMode))
        {
            responseMode = ResponseMode.Query;
        }

        if (!authorizationSource.TryGetValue(OpenIdConstants.Parameters.RedirectUri, out var redirectUrl))
        {
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.RedirectUri)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri))
        {
            throw ErrorFactory
                .InvalidRequest("The specified 'redirect_uri' is not valid absolute URI.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        var clientSettings = openIdClient.Settings;
        var redirectUrls = openIdClient.RedirectUrls;
        var effectiveUrl = redirectUri.GetComponents(
            UriComponents.Scheme |
            UriComponents.Host |
            UriComponents.Port |
            UriComponents.Path,
            UriFormat.UriEscaped);

        var isSafe = (clientSettings.AllowLoopbackRedirect && redirectUri.IsLoopback) || redirectUrls.Contains(effectiveUrl);
        if (!isSafe)
        {
            throw ErrorFactory
                .UnauthorizedClient("The specified 'redirect_uri' is not valid for the associated 'client_id'.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return new ClientRedirectContext(redirectUri, responseMode, state);
    }

    private async ValueTask<IResult> ProcessAuthorizationRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        ClientRedirectContext clientRedirectContext,
        CancellationToken cancellationToken)
    {
        var mediator = openIdContext.Mediator;

        var responseMode = clientRedirectContext.ResponseMode;
        var redirectUri = clientRedirectContext.RedirectUri;

        // the request object may have changed the response mode
        var requestObject = authorizationRequest.OriginalRequestObject;
        if (requestObject?.ResponseMode is not null && requestObject.ResponseMode.Value != responseMode)
        {
            responseMode = requestObject.ResponseMode.Value;
        }

        await mediator.SendAsync(
            new ValidateAuthorizationRequestCommand(
                openIdContext,
                openIdClient,
                authorizationRequest),
            cancellationToken);

        // TODO: is it okay to reauthenticate after continuations?
        var authenticateResult = await mediator.SendAsync<AuthenticateCommand, AuthenticateSubjectResult>(
            new AuthenticateCommand(
                openIdContext,
                openIdClient,
                authorizationRequest),
            cancellationToken);

        if (!authenticateResult.IsSuccess)
        {
            var openIdError = ErrorFactory
                .AccessDenied("Unable to authenticate the end-user.")
                .WithState(clientRedirectContext.State)
                .WithException(authenticateResult.Error);

            return new AuthorizationResult(redirectUri, responseMode, openIdError);
        }

        var authenticationTicket = authenticateResult.Ticket.Value;

        // the following may require interaction which uses continuations
        var authorizeResult = await mediator.SendAsync<AuthorizeCommand, IResult?>(
            new AuthorizeCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket),
            cancellationToken);

        if (authorizeResult != null)
            return authorizeResult;

        var authorizationTicket = await mediator.SendAsync<CreateAuthorizationTicketCommand, IAuthorizationTicket>(
            new CreateAuthorizationTicketCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket),
            cancellationToken);

        return new AuthorizationResult(redirectUri, responseMode, authorizationTicket);
    }

    #region IContinueProvider

    /// <inheritdoc />
    public string ContinueCode => OpenIdConstants.ContinueCodes.Authorization;

    /// <inheritdoc />
    public async ValueTask<IResult> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayload,
        CancellationToken cancellationToken)
    {
        // TODO: extract additional parameters from the HTTP request

        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken);

        if (authResult.IsError)
        {
            return authResult.Error.AsResult();
        }

        if (authResult.IsUndefined)
        {
            return ErrorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsResult();
        }

        var openIdClient = authResult.PublicClient ??
                           authResult.ConfidentialClient ??
                           throw new InvalidOperationException();

        var authorizationRequest = continuePayload.Deserialize<IAuthorizationRequest>(OpenIdServer.JsonSerializerOptions);
        if (authorizationRequest == null)
            throw new InvalidOperationException("JSON deserialization returned null.");

        authorizationRequest.IsContinuation = true;

        var clientRedirectContext = GetClientRedirectContext(openIdClient, authorizationRequest);

        var result = await ProcessAuthorizationRequestAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            clientRedirectContext,
            cancellationToken);

        return result;
    }

    #endregion

    #region LoadAuthorizationSourceCommand

    /// <inheritdoc />
    public async ValueTask<IAuthorizationSource> HandleAsync(
        LoadAuthorizationSourceCommand command,
        CancellationToken cancellationToken)
    {
        var openIdContext = command.OpenIdContext;
        var httpContext = openIdContext.Http;
        var httpRequest = httpContext.Request;

        AuthorizationSourceType sourceType;
        IEnumerable<KeyValuePair<string, StringValues>> properties;

        if (HttpMethods.IsGet(httpRequest.Method))
        {
            sourceType = AuthorizationSourceType.Query;
            properties = httpRequest.Query;
        }
        else if (HttpMethods.IsPost(httpRequest.Method))
        {
            const string expectedContentType = "application/x-www-form-urlencoded";
            if (!httpRequest.ContentType?.StartsWith(expectedContentType, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                throw ErrorFactory
                    .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                    .WithDescription($"The content type of the request must be '{expectedContentType}'. Received '{httpRequest.ContentType}'.")
                    .WithStatusCode(StatusCodes.Status415UnsupportedMediaType)
                    .AsException();
            }

            sourceType = AuthorizationSourceType.Form;
            properties = await httpRequest.ReadFormAsync(cancellationToken);
        }
        else
        {
            throw ErrorFactory
                .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                .WithStatusCode(StatusCodes.Status405MethodNotAllowed)
                .AsException();
        }

        // we manually load the parameters without parsing because that will occur later
        var parameters = properties.Select(property =>
        {
            var descriptor = new ParameterDescriptor(property.Key);
            return descriptor.Loader.Load(OpenIdServer, descriptor, property.Value);
        });

        var message = AuthorizationSource.Load(OpenIdServer, parameters);
        message.AuthorizationSourceType = sourceType;

        return message;
    }

    #endregion

    #region LoadAuthorizationRequestCommand

    /// <inheritdoc />
    public async ValueTask<IAuthorizationRequest> HandleAsync(
        LoadAuthorizationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (_, openIdClient, authorizationSource) = command;

        // the following will parse string-values into strongly-typed parameters and may throw
        var requestMessage = AuthorizationRequestMessage.Load(authorizationSource);
        requestMessage.AuthorizationSourceType = authorizationSource.AuthorizationSourceType;

        // TODO: add support for OAuth 2.0 Pushed Authorization Requests (PAR)
        // https://datatracker.ietf.org/doc/html/rfc9126

        var requestObject = await LoadRequestObjectAsync(
            openIdClient,
            requestMessage,
            cancellationToken);

        const bool isContinuation = false;
        var authorizationRequest = new AuthorizationRequest(
            isContinuation,
            requestMessage,
            requestObject);

        return authorizationRequest;
    }

    private async ValueTask<IAuthorizationRequestObject?> LoadRequestObjectAsync(
        OpenIdClient openIdClient,
        IAuthorizationRequestMessage requestMessage,
        CancellationToken cancellationToken)
    {
        var requestJwt = requestMessage.RequestJwt;
        var requestUri = requestMessage.RequestUri;

        RequestObjectSource requestObjectSource;
        string errorCode;

        if (requestUri is not null)
        {
            if (!openIdClient.Settings.RequestUriParameterSupported)
                throw ErrorFactory
                    .RequestUriNotSupported()
                    .AsException();

            requestObjectSource = RequestObjectSource.Remote;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri;

            if (!string.IsNullOrEmpty(requestJwt))
                throw ErrorFactory
                    .InvalidRequest("Both the 'request' and 'request_uri' parameters cannot be present at the same time.", errorCode)
                    .AsException();

            requestJwt = await FetchRequestUriAsync(
                openIdClient,
                requestUri,
                cancellationToken);
        }
        else if (!string.IsNullOrEmpty(requestJwt))
        {
            if (!openIdClient.Settings.RequestParameterSupported)
                throw ErrorFactory
                    .RequestParameterNotSupported()
                    .AsException();

            requestObjectSource = RequestObjectSource.Inline;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestJwt;
        }
        else
        {
            return null;
        }

        JsonElement jwtPayload;
        try
        {
            var parameters = new ValidateJwtParameters()
                .UseValidationKeys(openIdClient.SecretKeys)
                .ValidateIssuer(openIdClient.ClientId)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime();

            var expectedAudience = openIdClient.Settings.RequestObjectExpectedAudience;
            if (!string.IsNullOrEmpty(expectedAudience))
                parameters.ValidateAudience(expectedAudience);

            var result = await JsonWebTokenService.ValidateJwtAsync(
                requestJwt,
                parameters,
                cancellationToken);

            if (!result.IsValid)
            {
                ExceptionDispatchInfo.Throw(result.Exception);
            }

            jwtPayload = result.DecodedJwt.Payload;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to decode JWT");
            throw ErrorFactory
                .FailedToDecodeJwt(errorCode)
                .WithException(exception)
                .AsException();
        }

        try
        {
            // this will deserialize the object using: OpenIdMessageJsonConverterFactory => OpenIdMessageJsonConverter => OpenIdMessage.Load
            var requestObject = jwtPayload.Deserialize<AuthorizationRequestObject>(OpenIdServer.JsonSerializerOptions);
            if (requestObject == null)
                throw new InvalidOperationException("JSON deserialization returned null.");

            requestObject.RequestObjectSource = requestObjectSource;

            return requestObject;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to deserialize JSON");
            throw ErrorFactory
                .FailedToDeserializeJson(errorCode)
                .WithException(exception)
                .AsException();
        }
    }

    private async ValueTask<string> FetchRequestUriAsync(
        OpenIdClient openIdClient,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        var clientSettings = openIdClient.Settings;

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        request.Options.Set(new HttpRequestOptionsKey<OpenIdClient>("OpenIdClient"), openIdClient);

        using var httpClient = HttpClientFactory.CreateClient();

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw ErrorFactory
                    .InvalidRequestUri($"The http status code of the response must be 200 OK. Received {(int)response.StatusCode} {response.StatusCode}.")
                    .AsException();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var expectedContentType = clientSettings.RequestUriExpectedContentType;
            if (clientSettings.RequestUriRequireStrictContentType && !string.Equals(contentType, expectedContentType, StringComparison.Ordinal))
                throw ErrorFactory
                    .InvalidRequestUri($"The content type of the response must be '{expectedContentType}'. Received '{contentType}'.")
                    .AsException();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to fetch the request URI");
            throw ErrorFactory
                .InvalidRequestUri("Failed to fetch the request URI")
                .WithException(exception)
                .AsException();
        }
    }

    #endregion

    #region ValidateAuthorizationRequestCommand

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateAuthorizationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (_, openIdClient, authorizationRequest) = command;

        var requestMessage = authorizationRequest.OriginalRequestMessage;
        var requestObject = authorizationRequest.OriginalRequestObject;

        ValidateRequestMessage(requestMessage);

        if (requestObject != null)
            ValidateRequestObject(requestMessage, requestObject);

        ValidateRequest(openIdClient, authorizationRequest);

        return ValueTask.CompletedTask;
    }

    [AssertionMethod]
    private void ValidateRequestMessage(
        IAuthorizationRequestMessage requestMessage)
    {
        var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
        if (responseType == ResponseTypes.Unspecified)
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.ResponseType).AsException();

        if (responseType.HasFlag(ResponseTypes.None) && responseType != ResponseTypes.None)
            throw ErrorFactory.InvalidRequest("The 'none' response_type must not be combined with other values.").AsException();

        var redirectUri = requestMessage.RedirectUri;
        if (redirectUri is null)
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.RedirectUri).AsException();
    }

    [AssertionMethod]
    private void ValidateRequestObject(
        IAuthorizationRequestMessage requestMessage,
        IAuthorizationRequestObject requestObject)
    {
        var errorCode = requestObject.RequestObjectSource == RequestObjectSource.Remote ?
            OpenIdConstants.ErrorCodes.InvalidRequestUri :
            OpenIdConstants.ErrorCodes.InvalidRequestJwt;

        /*
         * request and request_uri parameters MUST NOT be included in Request Objects.
         */

        if (requestObject.ContainsKey(OpenIdConstants.Parameters.Request))
            throw ErrorFactory.InvalidRequest("The JWT request object must not contain the 'request' parameter.", errorCode).AsException();

        if (requestObject.ContainsKey(OpenIdConstants.Parameters.RequestUri))
            throw ErrorFactory.InvalidRequest("The JWT request object must not contain the 'request_uri' parameter.", errorCode).AsException();

        /*
         * So that the request is a valid OAuth 2.0 Authorization Request, values for the response_type and client_id parameters MUST
         * be included using the OAuth 2.0 request syntax, since they are REQUIRED by OAuth 2.0. The values for these parameters MUST
         * match those in the Request Object, if present.
         */

        if (requestObject.ResponseType != null && requestObject.ResponseType != requestMessage.ResponseType)
            throw ErrorFactory.InvalidRequest("The 'response_type' parameter in the JWT request object must match the same value from the request message.", errorCode).AsException();

        /*
         * The Client ID values in the "client_id" request parameter and in the Request Object "client_id" claim MUST be identical.
         */

        if (string.IsNullOrEmpty(requestObject.ClientId))
            throw ErrorFactory.MissingParameter("The 'client_id' parameter in the JWT request object is missing.", errorCode).AsException();

        if (!string.Equals(requestObject.ClientId, requestMessage.ClientId, StringComparison.Ordinal))
            throw ErrorFactory.InvalidRequest("The 'client_id' parameter in the JWT request object must match the same value from the request message.", errorCode).AsException();
    }

    [AssertionMethod]
    private void ValidateRequest(
        OpenIdClient openIdClient,
        IAuthorizationRequest request)
    {
        var clientSettings = openIdClient.Settings;

        var hasOpenIdScope = request.Scopes.Contains(OpenIdConstants.ScopeTypes.OpenId);
        var isImplicit = request.GrantType == OpenIdConstants.GrantTypes.Implicit;
        var isHybrid = request.GrantType == OpenIdConstants.GrantTypes.Hybrid;

        var hasCodeChallenge = !string.IsNullOrEmpty(request.CodeChallenge);
        var codeChallengeMethodIsPlain = !hasCodeChallenge || request.CodeChallengeMethod == OpenIdConstants.CodeChallengeMethods.Plain;

        if (request.Scopes.Count == 0)
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.Scope).AsException();

        if (request.ResponseType == ResponseTypes.Unspecified)
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.ResponseType).AsException();

        if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(request.Nonce))
            throw ErrorFactory.MissingParameter(OpenIdConstants.Parameters.Nonce).AsException();

        if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && !hasOpenIdScope)
            throw ErrorFactory.InvalidRequest("The openid scope is required when requesting id tokens.").AsException();

        if (request.ResponseMode == ResponseMode.Query && request.GrantType != OpenIdConstants.GrantTypes.AuthorizationCode)
            throw ErrorFactory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.").AsException();

        if (request.PromptType.HasFlag(PromptTypes.None) && request.PromptType != PromptTypes.None)
            throw ErrorFactory.InvalidRequest("The 'none' prompt must not be combined with other values.").AsException();

        if (request.PromptType.HasFlag(PromptTypes.CreateAccount) && request.PromptType != PromptTypes.CreateAccount)
            throw ErrorFactory.InvalidRequest("The 'create' prompt must not be combined with other values.").AsException();

        if (hasOpenIdScope && string.IsNullOrEmpty(request.Nonce) && (isImplicit || isHybrid))
            throw ErrorFactory.InvalidRequest("The nonce parameter is required when using the implicit or hybrid flows for openid requests.").AsException();

        // perform configurable checks...

        // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
        if (request.ResponseType.HasFlag(ResponseTypes.Token) && !clientSettings.AllowUnsafeTokenResponse)
            throw ErrorFactory.UnauthorizedClient("The configuration prohibits the use of unsafe token responses.").AsException();

        // require_pkce
        if (!hasCodeChallenge && clientSettings.RequireCodeChallenge)
            throw ErrorFactory.UnauthorizedClient("The configuration requires the use of PKCE parameters.").AsException();

        // allow_plain_code_challenge_method
        if (codeChallengeMethodIsPlain && !clientSettings.AllowPlainCodeChallengeMethod)
            throw ErrorFactory.UnauthorizedClient("The configuration prohibits the plain PKCE method.").AsException();

        // acr_values_supported
        if (clientSettings.TryGet(KnownSettings.AcrValuesSupported.Key, out var acrValuesSupported))
        {
            var acrValues = request.AcrValues;
            if (acrValues.Count > 0 && !acrValues.Except(acrValuesSupported.Value).Any())
                throw ErrorFactory.NotSupported(OpenIdConstants.Parameters.AcrValues).AsException();
        }

        // claims_locales_supported
        if (clientSettings.TryGet(KnownSettings.ClaimsLocalesSupported.Key, out var claimsLocalesSupported))
        {
            var claimsLocales = request.ClaimsLocales;
            if (claimsLocales.Count > 0 && !claimsLocales.Except(claimsLocalesSupported.Value).Any())
                throw ErrorFactory.NotSupported(OpenIdConstants.Parameters.ClaimsLocales).AsException();
        }

        // claims_parameter_supported
        if (clientSettings.TryGet(KnownSettings.ClaimsParameterSupported.Key, out var claimsParameterSupported))
        {
            var claimCount = request.Claims?.UserInfo?.Count ?? 0 + request.Claims?.IdToken?.Count ?? 0;
            if (claimCount > 0 && !claimsParameterSupported.Value)
                throw ErrorFactory.NotSupported(OpenIdConstants.Parameters.Claims).AsException();
        }

        // display_values_supported
        if (clientSettings.TryGet(KnownSettings.DisplayValuesSupported.Key, out var displayValuesSupported))
        {
            if (!displayValuesSupported.Value.Contains(request.DisplayType))
                throw ErrorFactory.NotSupported(OpenIdConstants.Parameters.Display).AsException();
        }

        // grant_types_supported
        if (clientSettings.TryGet(KnownSettings.GrantTypesSupported.Key, out var grantTypesSupported))
        {
            if (!grantTypesSupported.Value.Contains(request.GrantType))
                throw ErrorFactory.NotSupported(OpenIdConstants.Parameters.GrantType).AsException();
        }

        // prompt_values_supported
        if (clientSettings.TryGet(KnownSettings.PromptValuesSupported.Key, out var promptValuesSupported))
        {
            /*
             * https://openid.net/specs/openid-connect-prompt-create-1_0.html#section-4.1
             *
             * If the OpenID Provider receives a prompt value that it does not support (not declared in the
             * prompt_values_supported metadata field) the OP SHOULD respond with an HTTP 400 (Bad Request)
             * status code and an error value of invalid_request. It is RECOMMENDED that the OP return an
             * error_description value identifying the invalid parameter value.
             */
            if (!promptValuesSupported.Value.Contains(request.PromptType))
                throw ErrorFactory
                    .NotSupported(OpenIdConstants.Parameters.Prompt)
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
        }

        // response_modes_supported
        if (clientSettings.TryGet(KnownSettings.ResponseModesSupported.Key, out var responseModesSupported))
        {
            if (!responseModesSupported.Value.Contains(request.ResponseMode))
                throw ErrorFactory.NotSupported(OpenIdConstants.Parameters.ResponseMode).AsException();
        }

        // response_types_supported
        if (clientSettings.TryGet(KnownSettings.ResponseTypesSupported.Key, out var responseTypesSupported))
        {
            if (!responseTypesSupported.Value.Contains(request.ResponseType))
                throw ErrorFactory.NotSupported(OpenIdConstants.Parameters.ResponseType).AsException();
        }

        // scopes_supported
        if (clientSettings.TryGet(KnownSettings.ScopesSupported.Key, out var scopesSupported))
        {
            var scopes = request.Scopes;
            if (scopes.Count > 0 && !scopes.Except(scopesSupported.Value).Any())
                throw ErrorFactory.NotSupported(OpenIdConstants.Parameters.Scope).AsException();
        }

        // subject_types_supported

        // token_endpoint_auth_methods_supported
        // token_endpoint_auth_signing_alg_values_supported

        // ui_locales_supported

        // other checks...

        // TODO: check allowed IdP from client configuration

        // TODO: add support for Resource Indicators
        // https://datatracker.ietf.org/doc/html/rfc8707

        // TODO: check session cookie
    }

    #endregion

    #region AuthenticateCommand

    /// <inheritdoc />
    public async ValueTask<AuthenticateSubjectResult> HandleAsync(
        AuthenticateCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, _) = command;

        var httpContext = openIdContext.Http;

        var signInSchemeName = openIdClient.Settings.AuthorizationSignInScheme;
        if (string.IsNullOrEmpty(signInSchemeName))
        {
            if (!DefaultSignInSchemeFetched)
            {
                var signInScheme = await AuthenticationSchemeProvider.GetDefaultSignInSchemeAsync();
                DefaultSignInSchemeName = signInScheme?.Name;
                DefaultSignInSchemeFetched = true;
            }

            signInSchemeName = DefaultSignInSchemeName;
        }

        var baseResult = await httpContext.AuthenticateAsync(signInSchemeName);

        if (baseResult.None)
        {
            return default;
        }

        if (baseResult.Failure is not null)
        {
            return new AuthenticateSubjectResult(baseResult.Failure);
        }

        Debug.Assert(baseResult.Succeeded);

        var baseTicket = baseResult.Ticket;
        var authenticationScheme = baseTicket.AuthenticationScheme;
        var authenticationProperties = baseTicket.Properties;

        var subject = baseTicket.Principal.Identity as ClaimsIdentity ??
                      throw new InvalidOperationException("The AuthenticationTicket must contain a ClaimsIdentity.");

        var subjectId = subject.Claims.GetSubjectIdOrDefault() ??
                        throw new InvalidOperationException("The authenticated identity must contain a 'sub' claim.");

        var ticket = new SubjectAuthentication(
            authenticationScheme,
            authenticationProperties,
            subject,
            subjectId);

        return new AuthenticateSubjectResult(ticket);
    }

    #endregion

    #region AuthorizeCommand

    /// <inheritdoc />
    public async ValueTask<IResult?> HandleAsync(
        AuthorizeCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, authorizationRequest, authenticationTicket) = command;

        var clientSettings = openIdClient.Settings;
        var isContinuation = authorizationRequest.IsContinuation;

        // prevent infinite loops from continuations and user interaction
        var effectivePromptType = isContinuation ?
            PromptTypes.None :
            authorizationRequest.PromptType;

        if (effectivePromptType.HasFlag(PromptTypes.CreateAccount))
        {
            Logger.LogInformation("Client requested account creation.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetCreateAccountUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        var reAuthenticate =
            effectivePromptType.HasFlag(PromptTypes.Login) ||
            effectivePromptType.HasFlag(PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            Logger.LogInformation("Client requested re-authentication.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        if (authenticationTicket is not { Subject.IsAuthenticated: true })
        {
            if (effectivePromptType.HasFlag(PromptTypes.None))
            {
                var error = ErrorFactory.LoginRequired();
                var redirectUri = authorizationRequest.RedirectUri;
                var responseMode = authorizationRequest.ResponseMode;
                return new AuthorizationResult(redirectUri, responseMode, error);
            }

            Logger.LogInformation("User not authenticated.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        if (!await ValidateUserIsActiveAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket,
                cancellationToken))
        {
            if (effectivePromptType.HasFlag(PromptTypes.None))
            {
                var error = ErrorFactory.LoginRequired();
                var redirectUri = authorizationRequest.RedirectUri;
                var responseMode = authorizationRequest.ResponseMode;
                return new AuthorizationResult(redirectUri, responseMode, error);
            }

            Logger.LogInformation("User not active.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        // TODO: check that tenant from subject matches the current tenant

        // TODO: check that IdP from subject matches the request's ACR
        // example acr value: urn:ncode:oidc:idp:google

        // TODO: check consent

        // check MaxAge
        if (!ValidateMaxAge(authenticationTicket.Subject, authorizationRequest.MaxAge, clientSettings.ClockSkew))
        {
            if (effectivePromptType.HasFlag(PromptTypes.None))
            {
                var error = ErrorFactory.LoginRequired();
                var redirectUri = authorizationRequest.RedirectUri;
                var responseMode = authorizationRequest.ResponseMode;
                return new AuthorizationResult(redirectUri, responseMode, error);
            }

            Logger.LogInformation("MaxAge exceeded.");

            var continueUrl = await ContinueService.GetContinueUrlAsync(
                openIdContext,
                OpenIdConstants.ContinueCodes.Authorization,
                authorizationRequest.ClientId,
                subjectId: null,
                clientSettings.ContinueAuthorizationLifetime,
                authorizationRequest,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        // TODO: check local idp restrictions

        // TODO: check external idp restrictions

        // TODO: check client's user SSO timeout

        return null;
    }

    private static async ValueTask<bool> ValidateUserIsActiveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken)
    {
        var result = new ValidateUserIsActiveResult();
        var command = new ValidateUserIsActiveCommand(
            openIdContext,
            openIdClient,
            authorizationRequest,
            subjectAuthentication,
            result);

        var mediator = openIdContext.Mediator;
        await mediator.SendAsync(command, cancellationToken);

        return result.IsActive;
    }

    private bool ValidateMaxAge(ClaimsIdentity subject, TimeSpan? maxAge, TimeSpan clockSkew)
    {
        /*
         * max_age
         * OPTIONAL. Maximum Authentication Age. Specifies the allowable elapsed time in seconds since the last time the
         * End-User was actively authenticated by the OP. If the elapsed time is greater than this value, the OP MUST attempt
         * to actively re-authenticate the End-User. (The max_age request parameter corresponds to the OpenID 2.0 PAPE
         * [OpenID.PAPE] max_auth_age request parameter.) When max_age is used, the ID Token returned MUST include an auth_time
         * Claim Value.
         */

        if (!maxAge.HasValue)
        {
            return true;
        }

        var authTimeClaim = subject.FindFirst(JoseClaimNames.Payload.AuthTime);
        if (authTimeClaim is null || !long.TryParse(authTimeClaim.Value, out var authTimeSeconds))
        {
            return false;
        }

        // use 'long/ticks' vs 'DateTime/DateTimeOffset' comparisons to avoid overflow exceptions

        var nowTicks = TimeProvider.GetTimestampWithPrecisionInSeconds();

        var authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds);
        var authTimeTicks = authTime.UtcTicks;

        var maxAgeTicks = maxAge.Value.Ticks;
        var clockSkewTicks = clockSkew.Ticks;

        return nowTicks <= authTimeTicks + maxAgeTicks + clockSkewTicks;
    }

    #endregion

    #region CreateAuthorizationTicketCommand

    /// <inheritdoc />
    public async ValueTask<IAuthorizationTicket> HandleAsync(
        CreateAuthorizationTicketCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, authorizationRequest, subjectAuthentication) = command;

        var responseType = authorizationRequest.ResponseType;

        var ticket = AuthorizationTicket.Create(OpenIdServer);

        ticket.CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        ticket.State = authorizationRequest.State;
        ticket.Issuer = openIdContext.Tenant.Issuer;

        if (responseType.HasFlag(ResponseTypes.Code))
        {
            var securityToken = await AuthorizationCodeService.CreateAuthorizationCodeAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                subjectAuthentication,
                cancellationToken);

            ticket.AuthorizationCode = securityToken.TokenValue;
        }

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = ticket.CreatedWhen,
            GrantType = authorizationRequest.GrantType,
            Nonce = authorizationRequest.Nonce,
            State = authorizationRequest.State,
            Scopes = authorizationRequest.Scopes,
            AuthorizationCode = ticket.AuthorizationCode,
            SubjectAuthentication = subjectAuthentication
        };

        if (responseType.HasFlag(ResponseTypes.Token))
        {
            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                securityTokenRequest,
                cancellationToken);

            ticket.AccessToken = securityToken.TokenValue;
            ticket.ExpiresIn = securityToken.TokenPeriod.Duration;
            ticket.TokenType = OpenIdConstants.TokenTypes.Bearer; // TODO: add support for DPoP
        }

        if (responseType.HasFlag(ResponseTypes.IdToken))
        {
            var newRequest = securityTokenRequest with
            {
                AccessToken = ticket.AccessToken
            };

            var securityToken = await TokenService.CreateIdTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken);

            ticket.IdToken = securityToken.TokenValue;
        }

        return ticket;
    }

    #endregion
}
