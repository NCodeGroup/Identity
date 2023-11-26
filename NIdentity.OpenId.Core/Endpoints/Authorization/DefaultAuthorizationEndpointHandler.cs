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
using NCode.Identity;
using NCode.Identity.JsonWebTokens;
using NCode.Jose;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Exceptions;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Logic.Authorization;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Settings;
using NIdentity.OpenId.Stores;
using ISystemClock = NIdentity.OpenId.Logic.ISystemClock;

namespace NIdentity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the authorization endpoint.
/// </summary>
public class DefaultAuthorizationEndpointHandler :
    IOpenIdEndpointProvider,
    ICommandResponseHandler<LoadAuthorizationSourceCommand, IAuthorizationSource>,
    ICommandResponseHandler<LoadAuthorizationRequestCommand, AuthorizationContext>,
    ICommandHandler<ValidateAuthorizationRequestCommand>,
    ICommandResponseHandler<AuthenticateCommand, AuthenticateResult>,
    ICommandResponseHandler<AuthorizeCommand, IResult?>,
    ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private bool DefaultSignInSchemeFetched { get; set; }
    private string? DefaultSignInSchemeName { get; set; }

    private ILogger<DefaultAuthorizationEndpointHandler> Logger { get; }
    private ISystemClock SystemClock { get; }
    private IHttpClientFactory HttpClientFactory { get; }
    private ISecretSerializer SecretSerializer { get; }
    private IJsonWebTokenService JsonWebTokenService { get; }
    private IClientStore ClientStore { get; }
    private IAuthenticationSchemeProvider AuthenticationSchemeProvider { get; }
    private IAuthorizationCallbackService CallbackService { get; }
    private IAuthorizationInteractionService InteractionService { get; }
    private IAuthorizationTicketService TicketService { get; }
    private IOpenIdContextFactory ContextFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAuthorizationEndpointHandler"/> class.
    /// </summary>
    public DefaultAuthorizationEndpointHandler(
        ILogger<DefaultAuthorizationEndpointHandler> logger,
        ISystemClock systemClock,
        IHttpClientFactory httpClientFactory,
        ISecretSerializer secretSerializer,
        IJsonWebTokenService jsonWebTokenService,
        IClientStore clientStore,
        IAuthenticationSchemeProvider authenticationSchemeProvider,
        IAuthorizationCallbackService callbackService,
        IAuthorizationInteractionService interactionService,
        IAuthorizationTicketService ticketService,
        IOpenIdContextFactory contextFactory)
    {
        Logger = logger;
        SystemClock = systemClock;
        HttpClientFactory = httpClientFactory;
        SecretSerializer = secretSerializer;
        JsonWebTokenService = jsonWebTokenService;
        ClientStore = clientStore;
        AuthenticationSchemeProvider = authenticationSchemeProvider;
        CallbackService = callbackService;
        InteractionService = interactionService;
        TicketService = ticketService;
        ContextFactory = contextFactory;
    }

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapMethods(
            OpenIdConstants.EndpointPaths.Authorization,
            new[] { HttpMethods.Get, HttpMethods.Post },
            HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Authorization)
        .OpenIdDiscoverable();

    private async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken)
    {
        var propertyBag = new PropertyBag();

        var openIdContext = await ContextFactory.CreateContextAsync(
            httpContext,
            mediator,
            propertyBag,
            cancellationToken);

        var errorFactory = openIdContext.ErrorFactory;

        // for errors, before we redirect, we must validate the client_id and redirect_uri
        // otherwise we must return a failure HTTP status code

        // the following tries its best to not throw for OpenID protocol errors
        var authorizationSource = await mediator.SendAsync<LoadAuthorizationSourceCommand, IAuthorizationSource>(
            new LoadAuthorizationSourceCommand(openIdContext),
            cancellationToken);

        // this will throw if client_id or redirect_uri are invalid
        var clientRedirectContext = await GetClientRedirectContextAsync(
            errorFactory,
            authorizationSource,
            cancellationToken);

        // everything after this point is safe to redirect to the client

        var client = clientRedirectContext.Client;
        var clientSettings = clientRedirectContext.ClientSettings;
        var redirectUri = clientRedirectContext.RedirectUri;
        var responseMode = clientRedirectContext.ResponseMode;

        try
        {
            var authorizationContext = await mediator.SendAsync<LoadAuthorizationRequestCommand, AuthorizationContext>(
                new LoadAuthorizationRequestCommand(
                    authorizationSource,
                    client,
                    clientSettings),
                cancellationToken);

            // the request object may have changed the response mode
            var requestObject = authorizationContext.AuthorizationRequest.OriginalRequestObject;
            if (requestObject?.ResponseMode is not null && requestObject.ResponseMode.Value != responseMode)
            {
                responseMode = requestObject.ResponseMode.Value;
            }

            await mediator.SendAsync(
                new ValidateAuthorizationRequestCommand(authorizationContext),
                cancellationToken);

            var authenticateResult = await mediator.SendAsync<AuthenticateCommand, AuthenticateResult>(
                new AuthenticateCommand(authorizationContext),
                cancellationToken);

            if (!authenticateResult.Succeeded)
            {
                var openIdError = errorFactory
                    .AccessDenied("An error occured while attempting to authenticate the end-user.")
                    .WithState(clientRedirectContext.State)
                    .WithException(authenticateResult.Failure);

                return new AuthorizationResult(redirectUri, responseMode, openIdError);
            }

            var authenticationTicket = authenticateResult.Ticket;

            var authorizeResult = await mediator.SendAsync<AuthorizeCommand, IResult?>(
                new AuthorizeCommand(
                    authorizationContext,
                    authenticationTicket),
                cancellationToken);

            if (authorizeResult != null)
                return authorizeResult;

            var authorizationTicket = await mediator.SendAsync<CreateAuthorizationTicketCommand, IAuthorizationTicket>(
                new CreateAuthorizationTicketCommand(
                    authorizationContext,
                    authenticationTicket),
                cancellationToken);

            return new AuthorizationResult(redirectUri, responseMode, authorizationTicket);
        }
        catch (Exception exception)
        {
            var openIdError = exception is OpenIdException openIdException ?
                openIdException.Error :
                errorFactory
                    .Create(OpenIdConstants.ErrorCodes.ServerError)
                    .WithState(clientRedirectContext.State)
                    .WithException(exception);

            return new AuthorizationResult(redirectUri, responseMode, openIdError);
        }
    }

    private async ValueTask<ClientRedirectContext> GetClientRedirectContextAsync(
        IOpenIdErrorFactory errorFactory,
        IOpenIdMessage authorizationSource,
        CancellationToken cancellationToken)
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
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.RedirectUri)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        if (!Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri))
        {
            throw errorFactory
                .InvalidRequest("The specified 'redirect_uri' is not valid absolute URI.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        if (!authorizationSource.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientId))
        {
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.ClientId)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        if (StringValues.IsNullOrEmpty(clientId))
        {
            throw errorFactory
                .InvalidRequest("The specified 'client_id' cannot be null or empty.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        var tenantId = authorizationSource.OpenIdContext.Tenant.TenantId;
        var client = await ClientStore.TryGetByClientIdAsync(
            tenantId,
            clientId.ToString(),
            cancellationToken);

        if (client == null)
        {
            throw errorFactory
                .InvalidRequest("The specified 'client_id' is invalid.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        var mergedSettings = authorizationSource.OpenIdContext.Tenant.TenantSettings.Merge(client.Settings);
        var clientSettings = new KnownSettingCollection(mergedSettings);

        var isSafe = (clientSettings.AllowLoopbackRedirect && redirectUri.IsLoopback) || client.RedirectUris.Contains(redirectUri);
        if (!isSafe)
        {
            throw errorFactory
                .InvalidRequest("The specified 'redirect_uri' is not valid for the associated 'client_id'.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return new ClientRedirectContext
        {
            State = state,
            Client = client,
            ClientSettings = clientSettings,
            RedirectUri = redirectUri,
            ResponseMode = responseMode
        };
    }

    #region LoadAuthorizationSourceCommand

    /// <inheritdoc />
    public async ValueTask<IAuthorizationSource> HandleAsync(
        LoadAuthorizationSourceCommand command,
        CancellationToken cancellationToken)
    {
        var openIdContext = command.OpenIdContext;
        var errorFactory = openIdContext.ErrorFactory;

        var httpContext = openIdContext.HttpContext;
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
                throw errorFactory
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
            throw errorFactory
                .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                .WithStatusCode(StatusCodes.Status405MethodNotAllowed)
                .AsException();
        }

        // we manually load the parameters without parsing because that will occur later
        var parameters = properties.Select(property =>
        {
            var descriptor = new ParameterDescriptor(property.Key);
            return descriptor.Loader.Load(openIdContext, descriptor, property.Value);
        });

        var message = AuthorizationSource.Load(openIdContext, parameters);
        message.AuthorizationSourceType = sourceType;

        return message;
    }

    #endregion

    #region LoadAuthorizationRequestCommand

    /// <inheritdoc />
    public async ValueTask<AuthorizationContext> HandleAsync(
        LoadAuthorizationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationSource = command.AuthorizationSource;
        var client = command.Client;
        var clientSettings = command.ClientSettings;

        var openIdContext = authorizationSource.OpenIdContext;

        // the following will parse string-values into strongly-typed parameters and may throw
        var requestMessage = AuthorizationRequestMessage.Load(authorizationSource);
        requestMessage.AuthorizationSourceType = authorizationSource.AuthorizationSourceType;

        // TODO: add support for OAuth 2.0 Pushed Authorization Requests (PAR)
        // https://datatracker.ietf.org/doc/html/rfc9126

        var requestObject = await LoadRequestObjectAsync(
            requestMessage,
            client,
            clientSettings, cancellationToken);

        var authorizationRequest = new AuthorizationRequest(
            requestMessage,
            requestObject);

        var propertyBag = openIdContext.PropertyBag.Clone();
        return new DefaultAuthorizationContext(client, clientSettings, authorizationRequest, propertyBag);
    }

    private async ValueTask<IAuthorizationRequestObject?> LoadRequestObjectAsync(
        IAuthorizationRequestMessage requestMessage,
        Client client,
        IKnownSettingCollection clientSettings,
        CancellationToken cancellationToken)
    {
        var requestJwt = requestMessage.RequestJwt;
        var requestUri = requestMessage.RequestUri;
        var errorFactory = requestMessage.OpenIdContext.ErrorFactory;

        RequestObjectSource requestObjectSource;
        string errorCode;

        if (requestUri is not null)
        {
            if (!clientSettings.RequestUriParameterSupported)
                throw errorFactory
                    .RequestUriNotSupported()
                    .AsException();

            requestObjectSource = RequestObjectSource.Remote;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri;

            if (!string.IsNullOrEmpty(requestJwt))
                throw errorFactory
                    .InvalidRequest("Both the 'request' and 'request_uri' parameters cannot be present at the same time.", errorCode)
                    .AsException();

            requestJwt = await FetchRequestUriAsync(
                errorFactory,
                client,
                clientSettings,
                requestUri,
                cancellationToken);
        }
        else if (!string.IsNullOrEmpty(requestJwt))
        {
            if (!clientSettings.RequestParameterSupported)
                throw errorFactory
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
        IReadOnlyCollection<SecretKey> secretKeys = Array.Empty<SecretKey>();
        try
        {
            secretKeys = SecretSerializer.DeserializeSecrets(client.Secrets);

            var parameters = new ValidateJwtParameters()
                .UseValidationKeys(secretKeys)
                .ValidateIssuer(client.ClientId)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime();

            var expectedAudience = clientSettings.RequestObjectExpectedAudience;
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
            throw errorFactory
                .FailedToDecodeJwt(errorCode)
                .WithException(exception)
                .AsException();
        }
        finally
        {
            secretKeys.DisposeAll(ignoreExceptions: true);
        }

        try
        {
            // this will deserialize the object using: OpenIdMessageJsonConverterFactory => OpenIdMessageJsonConverter => OpenIdMessage.Load
            var requestObject = jwtPayload.Deserialize<AuthorizationRequestObject>(requestMessage.OpenIdContext.JsonSerializerOptions);
            if (requestObject == null)
                throw new InvalidOperationException("JSON deserialization returned null.");

            requestObject.RequestObjectSource = requestObjectSource;

            return requestObject;
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to deserialize JSON");
            throw errorFactory
                .FailedToDeserializeJson(errorCode)
                .WithException(exception)
                .AsException();
        }
    }

    private async ValueTask<string> FetchRequestUriAsync(
        IOpenIdErrorFactory errorFactory,
        Client client,
        IKnownSettingCollection clientSettings,
        Uri requestUri,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        request.Options.Set(new HttpRequestOptionsKey<Client>("Client"), client);

        using var httpClient = HttpClientFactory.CreateClient();

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw errorFactory
                    .InvalidRequestUri($"The http status code of the response must be 200 OK. Received {(int)response.StatusCode} {response.StatusCode}.")
                    .AsException();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var expectedContentType = clientSettings.RequestUriExpectedContentType;
            if (clientSettings.RequestUriRequireStrictContentType && !string.Equals(contentType, expectedContentType, StringComparison.Ordinal))
                throw errorFactory
                    .InvalidRequestUri($"The content type of the response must be '{expectedContentType}'. Received '{contentType}'.")
                    .AsException();

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.LogWarning(exception, "Failed to fetch the request URI");
            throw errorFactory
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
        var authorizationContext = command.AuthorizationContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;

        var requestMessage = authorizationRequest.OriginalRequestMessage;
        var requestObject = authorizationRequest.OriginalRequestObject;
        var client = authorizationContext.Client;
        var clientSettings = authorizationContext.ClientSettings;

        ValidateRequestMessage(requestMessage);

        if (requestObject != null)
            ValidateRequestObject(requestMessage, requestObject);

        ValidateRequest(client, clientSettings, authorizationRequest);

        return ValueTask.CompletedTask;
    }

    [AssertionMethod]
    private static void ValidateRequestMessage(
        IAuthorizationRequestMessage requestMessage)
    {
        var errorFactory = requestMessage.OpenIdContext.ErrorFactory;

        var responseType = requestMessage.ResponseType ?? ResponseTypes.Unspecified;
        if (responseType == ResponseTypes.Unspecified)
            throw errorFactory.MissingParameter(OpenIdConstants.Parameters.ResponseType).AsException();

        if (responseType.HasFlag(ResponseTypes.None) && responseType != ResponseTypes.None)
            throw errorFactory.InvalidRequest("The 'none' response_type must not be combined with other values.").AsException();

        var redirectUri = requestMessage.RedirectUri;
        if (redirectUri is null)
            throw errorFactory.MissingParameter(OpenIdConstants.Parameters.RedirectUri).AsException();
    }

    [AssertionMethod]
    private static void ValidateRequestObject(
        IAuthorizationRequestMessage requestMessage,
        IAuthorizationRequestObject requestObject)
    {
        var errorFactory = requestMessage.OpenIdContext.ErrorFactory;

        var errorCode = requestObject.RequestObjectSource == RequestObjectSource.Remote ?
            OpenIdConstants.ErrorCodes.InvalidRequestUri :
            OpenIdConstants.ErrorCodes.InvalidRequestJwt;

        /*
         * request and request_uri parameters MUST NOT be included in Request Objects.
         */

        if (requestObject.ContainsKey(OpenIdConstants.Parameters.Request))
            throw errorFactory.InvalidRequest("The JWT request object must not contain the 'request' parameter.", errorCode).AsException();

        if (requestObject.ContainsKey(OpenIdConstants.Parameters.RequestUri))
            throw errorFactory.InvalidRequest("The JWT request object must not contain the 'request_uri' parameter.", errorCode).AsException();

        /*
         * So that the request is a valid OAuth 2.0 Authorization Request, values for the response_type and client_id parameters MUST
         * be included using the OAuth 2.0 request syntax, since they are REQUIRED by OAuth 2.0. The values for these parameters MUST
         * match those in the Request Object, if present.
         */

        if (requestObject.ResponseType != null && requestObject.ResponseType != requestMessage.ResponseType)
            throw errorFactory.InvalidRequest("The 'response_type' parameter in the JWT request object must match the same value from the request message.", errorCode).AsException();

        /*
         * The Client ID values in the "client_id" request parameter and in the Request Object "client_id" claim MUST be identical.
         */

        if (string.IsNullOrEmpty(requestObject.ClientId))
            throw errorFactory.MissingParameter("The 'client_id' parameter in the JWT request object is missing.", errorCode).AsException();

        if (!string.Equals(requestObject.ClientId, requestMessage.ClientId, StringComparison.Ordinal))
            throw errorFactory.InvalidRequest("The 'client_id' parameter in the JWT request object must match the same value from the request message.", errorCode).AsException();
    }

    [AssertionMethod]
    private static void ValidateRequest(
        Client client,
        IKnownSettingCollection clientSettings,
        IAuthorizationRequest request)
    {
        var openIdContext = request.OpenIdContext;
        var errorFactory = openIdContext.ErrorFactory;

        var hasOpenIdScope = request.Scopes.Contains(OpenIdConstants.ScopeTypes.OpenId);
        var isImplicit = request.GrantType == GrantType.Implicit;
        var isHybrid = request.GrantType == GrantType.Hybrid;

        var hasCodeChallenge = !string.IsNullOrEmpty(request.CodeChallenge);
        var codeChallengeMethodIsPlain = request.CodeChallengeMethod == CodeChallengeMethod.Plain;

        if (client.IsDisabled)
            throw errorFactory.UnauthorizedClient("The client is disabled.").AsException();

        if (request.Scopes.Count == 0)
            throw errorFactory.MissingParameter(OpenIdConstants.Parameters.Scope).AsException();

        if (request.ResponseType == ResponseTypes.Unspecified)
            throw errorFactory.MissingParameter(OpenIdConstants.Parameters.ResponseType).AsException();

        if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && string.IsNullOrEmpty(request.Nonce))
            throw errorFactory.MissingParameter(OpenIdConstants.Parameters.Nonce).AsException();

        if (request.ResponseType.HasFlag(ResponseTypes.IdToken) && !hasOpenIdScope)
            throw errorFactory.InvalidRequest("The openid scope is required when requesting id tokens.").AsException();

        if (request.ResponseMode == ResponseMode.Query && request.GrantType != GrantType.AuthorizationCode)
            throw errorFactory.InvalidRequest("The 'query' encoding is only allowed for the authorization code grant.").AsException();

        if (request.PromptType.HasFlag(PromptTypes.None) && request.PromptType != PromptTypes.None)
            throw errorFactory.InvalidRequest("The 'none' prompt must not be combined with other values.").AsException();

        if (request.PromptType.HasFlag(PromptTypes.CreateAccount) && request.PromptType != PromptTypes.CreateAccount)
            throw errorFactory.InvalidRequest("The 'create' prompt must not be combined with other values.").AsException();

        if (hasOpenIdScope && string.IsNullOrEmpty(request.Nonce) && (isImplicit || isHybrid))
            throw errorFactory.InvalidRequest("The nonce parameter is required when using the implicit or hybrid flows for openid requests.").AsException();

        // perform configurable checks...

        // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
        if (request.ResponseType.HasFlag(ResponseTypes.Token) && !clientSettings.AllowUnsafeTokenResponse)
            throw errorFactory.UnauthorizedClient("The configuration prohibits the use of unsafe token responses.").AsException();

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (!hasCodeChallenge && clientSettings.RequireCodeChallenge)
            throw errorFactory.UnauthorizedClient("The configuration requires the use of PKCE parameters.").AsException();

        if (hasCodeChallenge && codeChallengeMethodIsPlain && !clientSettings.AllowPlainCodeChallengeMethod)
            throw errorFactory.UnauthorizedClient("The configuration prohibits the plain PKCE method.").AsException();

        // acr_values_supported
        if (clientSettings.TryGet(KnownSettings.AcrValuesSupported.Key, out var acrValuesSupported))
        {
            var acrValues = request.AcrValues;
            if (acrValues.Count > 0 && !acrValues.Except(acrValuesSupported.Value).Any())
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.AcrValues).AsException();
        }

        // claims_locales_supported
        if (clientSettings.TryGet(KnownSettings.ClaimsLocalesSupported.Key, out var claimsLocalesSupported))
        {
            var claimsLocales = request.ClaimsLocales;
            if (claimsLocales.Count > 0 && !claimsLocales.Except(claimsLocalesSupported.Value).Any())
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.ClaimsLocales).AsException();
        }

        // claims_parameter_supported
        if (clientSettings.TryGet(KnownSettings.ClaimsParameterSupported.Key, out var claimsParameterSupported))
        {
            var claimCount = request.Claims?.UserInfo?.Count ?? 0 + request.Claims?.IdToken?.Count ?? 0;
            if (claimCount > 0 && !claimsParameterSupported.Value)
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.Claims).AsException();
        }

        // display_values_supported
        if (clientSettings.TryGet(KnownSettings.DisplayValuesSupported.Key, out var displayValuesSupported))
        {
            if (!displayValuesSupported.Value.Contains(request.DisplayType))
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.Display).AsException();
        }

        // grant_types_supported
        if (clientSettings.TryGet(KnownSettings.GrantTypesSupported.Key, out var grantTypesSupported))
        {
            if (!grantTypesSupported.Value.Contains(request.GrantType))
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.GrantType).AsException();
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
                throw errorFactory
                    .NotSupported(OpenIdConstants.Parameters.Prompt)
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
        }

        // response_modes_supported
        if (clientSettings.TryGet(KnownSettings.ResponseModesSupported.Key, out var responseModesSupported))
        {
            if (!responseModesSupported.Value.Contains(request.ResponseMode))
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.ResponseMode).AsException();
        }

        // response_types_supported
        if (clientSettings.TryGet(KnownSettings.ResponseTypesSupported.Key, out var responseTypesSupported))
        {
            if (!responseTypesSupported.Value.Contains(request.ResponseType))
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.ResponseType).AsException();
        }

        // scopes_supported
        if (clientSettings.TryGet(KnownSettings.ScopesSupported.Key, out var scopesSupported))
        {
            var scopes = request.Scopes;
            if (scopes.Count > 0 && !scopes.Except(scopesSupported.Value).Any())
                throw errorFactory.NotSupported(OpenIdConstants.Parameters.Scope).AsException();
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
    public async ValueTask<AuthenticateResult> HandleAsync(
        AuthenticateCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationContext = command.AuthorizationContext;
        var clientSettings = authorizationContext.ClientSettings;
        var httpContext = authorizationContext.AuthorizationRequest.OpenIdContext.HttpContext;

        var signInSchemeName = clientSettings.AuthorizationSignInScheme;
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

        return await httpContext.AuthenticateAsync(signInSchemeName);
    }

    #endregion

    #region AuthorizeCommand

    /// <inheritdoc />
    public async ValueTask<IResult?> HandleAsync(
        AuthorizeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationContext = command.AuthorizationContext;
        var authenticationTicket = command.AuthenticationTicket;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var openIdContext = authorizationRequest.OpenIdContext;
        var clientSettings = authorizationContext.ClientSettings;
        var errorFactory = openIdContext.ErrorFactory;

        var promptType = authorizationRequest.PromptType;

        if (promptType.HasFlag(PromptTypes.CreateAccount))
        {
            // TODO: redirect to create account page
            throw new NotImplementedException();
        }

        var reAuthenticate =
            promptType.HasFlag(PromptTypes.Login) ||
            promptType.HasFlag(PromptTypes.SelectAccount);

        // TODO: remove 'prompt' parameter to prevent infinite loop

        if (reAuthenticate)
        {
            Logger.LogInformation("Client requested re-authentication.");

            var continueUrl = await CallbackService.GetContinueUrlAsync(
                authorizationContext,
                cancellationToken);

            var loginUrl = await InteractionService.GetLoginUrlAsync(
                authorizationContext,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = loginUrl };
        }

        if (authenticationTicket is not { Principal.Identity.IsAuthenticated: true })
        {
            if (promptType.HasFlag(PromptTypes.None))
            {
                var error = errorFactory.LoginRequired();
                var redirectUri = authorizationRequest.RedirectUri;
                var responseMode = authorizationRequest.ResponseMode;
                return new AuthorizationResult(redirectUri, responseMode, error);
            }

            Logger.LogInformation("User not authenticated.");

            var continueUrl = await CallbackService.GetContinueUrlAsync(
                authorizationContext,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                authorizationContext,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        var subject = authenticationTicket.Principal.Identity as ClaimsIdentity ??
                      throw new InvalidOperationException("The AuthenticationTicket must contain a ClaimsIdentity.");

        if (!await ValidateUserIsActiveAsync(authorizationContext, authenticationTicket, cancellationToken))
        {
            Logger.LogInformation("User not active.");

            var continueUrl = await CallbackService.GetContinueUrlAsync(
                authorizationContext,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                authorizationContext,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        // TODO: check that tenant from subject matches the current tenant

        // TODO: check that IdP from subject matches the request's ACR
        // example acr value: urn:ncode:oidc:idp:google

        // TODO: check consent

        // check MaxAge
        if (!ValidateMaxAge(subject, authorizationRequest.MaxAge, clientSettings.ClockSkew))
        {
            Logger.LogInformation("MaxAge exceeded.");

            var continueUrl = await CallbackService.GetContinueUrlAsync(
                authorizationContext,
                cancellationToken);

            var redirectUrl = await InteractionService.GetLoginUrlAsync(
                authorizationContext,
                continueUrl,
                cancellationToken);

            return new OpenIdRedirectResult { RedirectUrl = redirectUrl };
        }

        // TODO: check local idp restrictions

        // TODO: check external idp restrictions

        // TODO: check client's user SSO timeout

        await ValueTask.CompletedTask;
        return null;
    }

    private async ValueTask<bool> ValidateUserIsActiveAsync(
        AuthorizationContext authorizationContext,
        AuthenticationTicket authenticationTicket,
        CancellationToken cancellationToken)
    {
        var mediator = authorizationContext.AuthorizationRequest.OpenIdContext.Mediator;
        var command = new ValidateUserIsActiveCommand(authorizationContext, authenticationTicket);
        await mediator.SendAsync(command, cancellationToken);
        return command.IsActive;
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

        var now = SystemClock.UtcNow;
        var nowTicks = now.UtcTicks;

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
        var authorizationContext = command.AuthorizationContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var openIdContext = authorizationRequest.OpenIdContext;
        var responseType = authorizationRequest.ResponseType;

        var ticket = AuthorizationTicket.Create(openIdContext);

        ticket.CreatedWhen = SystemClock.UtcNow;
        ticket.State = authorizationRequest.State;
        ticket.Issuer = openIdContext.Tenant.Issuer;

        if (responseType.HasFlag(ResponseTypes.Code))
        {
            await TicketService.CreateAuthorizationCodeAsync(
                command,
                ticket,
                cancellationToken);
        }

        if (responseType.HasFlag(ResponseTypes.Token))
        {
            await TicketService.CreateAccessTokenAsync(
                command,
                ticket,
                cancellationToken);
        }

        if (responseType.HasFlag(ResponseTypes.IdToken))
        {
            await TicketService.CreateIdTokenAsync(
                command,
                ticket,
                cancellationToken);
        }

        return ticket;
    }

    #endregion
}
