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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
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
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;
using ISystemClock = NIdentity.OpenId.Logic.ISystemClock;

namespace NIdentity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the authorization endpoint.
/// </summary>
public class DefaultAuthorizationEndpointHandler :
    ICommandResponseHandler<AuthorizationEndpointCommand, IOpenIdResult>,
    ICommandResponseHandler<LoadAuthorizationSourceCommand, IAuthorizationSource>,
    ICommandResponseHandler<LoadAuthorizationRequestCommand, AuthorizationContext>,
    ICommandHandler<ValidateAuthorizationRequestCommand>,
    ICommandResponseHandler<AuthenticateCommand, AuthenticateResult>,
    ICommandResponseHandler<AuthorizeCommand, IOpenIdResult?>,
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
    private IAuthorizationLoginService LoginService { get; }
    private IAuthorizationTicketService TicketService { get; }

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
        IAuthorizationLoginService loginService,
        IAuthorizationTicketService ticketService)
    {
        Logger = logger;
        SystemClock = systemClock;
        HttpClientFactory = httpClientFactory;
        SecretSerializer = secretSerializer;
        JsonWebTokenService = jsonWebTokenService;
        ClientStore = clientStore;
        AuthenticationSchemeProvider = authenticationSchemeProvider;
        CallbackService = callbackService;
        LoginService = loginService;
        TicketService = ticketService;
    }

    #region AuthorizationEndpointCommand

    /// <inheritdoc />
    public async ValueTask<IOpenIdResult> HandleAsync(
        AuthorizationEndpointCommand command,
        CancellationToken cancellationToken)
    {
        var openIdContext = command.OpenIdContext;
        var errorFactory = openIdContext.ErrorFactory;
        var mediator = openIdContext.Mediator;

        var authorizationSource = await mediator.SendAsync<LoadAuthorizationSourceCommand, IAuthorizationSource>(
            new LoadAuthorizationSourceCommand(openIdContext),
            cancellationToken);

        // for errors, before we redirect, we must validate the client_id and redirect_uri
        // otherwise we must return a failure HTTP status code

        AuthorizationContext authorizationContext;
        try
        {
            authorizationContext = await mediator.SendAsync<LoadAuthorizationRequestCommand, AuthorizationContext>(
                new LoadAuthorizationRequestCommand(authorizationSource),
                cancellationToken);
        }
        catch (Exception exception)
        {
            var openIdError = exception is OpenIdException openIdException ?
                openIdException.Error :
                errorFactory
                    .Create(OpenIdConstants.ErrorCodes.ServerError)
                    .WithException(exception);

            var clientRedirectContext = await GetClientRedirectContextAsync(
                openIdError,
                authorizationSource,
                cancellationToken);

            return clientRedirectContext.IsSafe ?
                new AuthorizationResult(
                    clientRedirectContext.RedirectUri,
                    clientRedirectContext.ResponseMode.Value,
                    openIdError) :
                new OpenIdErrorResult(openIdError);
        }

        // everything after this point is safe to redirect to the client

        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var redirectUri = authorizationRequest.RedirectUri;
        var responseMode = authorizationRequest.ResponseMode;

        try
        {
            await mediator.SendAsync(
                new ValidateAuthorizationRequestCommand(authorizationContext),
                cancellationToken);

            var authenticateResult = await mediator.SendAsync<AuthenticateCommand, AuthenticateResult>(
                new AuthenticateCommand(openIdContext),
                cancellationToken);

            if (!authenticateResult.Succeeded)
            {
                var openIdError = errorFactory
                    .AccessDenied("An error occured while attempting to authenticate the end-user.")
                    .WithException(authenticateResult.Failure);

                return new AuthorizationResult(redirectUri, responseMode, openIdError);
            }

            var authenticationTicket = authenticateResult.Ticket;

            var authorizeResult = await mediator.SendAsync<AuthorizeCommand, IOpenIdResult?>(
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
                    .WithException(exception);

            return new AuthorizationResult(redirectUri, responseMode, openIdError);
        }
    }

    #endregion

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

        var message = AuthorizationSource.Load(openIdContext, properties);
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
        var openIdContext = authorizationSource.OpenIdContext;
        var errorFactory = openIdContext.ErrorFactory;
        var configuration = openIdContext.Tenant.Configuration.Authorization.RequestObject;

        var requestMessage = AuthorizationRequestMessage.Load(authorizationSource);
        requestMessage.AuthorizationSourceType = authorizationSource.AuthorizationSourceType;

        var client = await GetClientAsync(
            openIdContext.Tenant.TenantId,
            requestMessage.ClientId,
            errorFactory,
            cancellationToken);

        var isClientRedirectSafe = IsClientRedirectSafe(
            requestMessage.RedirectUri,
            client.AllowLoopback,
            client.RedirectUris);

        if (!isClientRedirectSafe)
            throw errorFactory
                .InvalidRequest($"The specified '{OpenIdConstants.Parameters.RedirectUri}' is not valid for this client application.")
                .WithClientRedirectContext(new ClientRedirectContext { IsSafe = false })
                .AsException();

        var clientRedirectContext = new ClientRedirectContext
        {
            IsSafe = true,
            RedirectUri = requestMessage.RedirectUri,
            ResponseMode = requestMessage.ResponseMode ?? ResponseMode.Query
        };

        try
        {
            var requestObject = await LoadRequestObjectAsync(
                configuration,
                requestMessage,
                client,
                cancellationToken);

            if (requestObject?.ResponseMode is not null && requestObject.ResponseMode.Value != clientRedirectContext.ResponseMode)
            {
                clientRedirectContext = clientRedirectContext with { ResponseMode = requestObject.ResponseMode.Value };
            }

            var authorizationRequest = new AuthorizationRequest(
                requestMessage,
                requestObject);

            var propertyBag = openIdContext.PropertyBag.Clone();
            return new DefaultAuthorizationContext(client, authorizationRequest, propertyBag);
        }
        catch (OpenIdException exception)
        {
            exception.Error.ClientRedirectContext = clientRedirectContext;
            throw;
        }
        catch (Exception exception)
        {
            throw errorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithException(exception)
                .WithClientRedirectContext(clientRedirectContext)
                .AsException();
        }
    }

    private async ValueTask<Client> GetClientAsync(
        string tenantId,
        string? clientId,
        IOpenIdErrorFactory errorFactory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(clientId))
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.ClientId)
                .WithClientRedirectContext(new ClientRedirectContext { IsSafe = false })
                .AsException();

        var client = await ClientStore.TryGetByClientIdAsync(tenantId, clientId, cancellationToken);
        if (client == null)
            throw errorFactory
                .InvalidRequest("The 'client_id' parameter is invalid.")
                .WithClientRedirectContext(new ClientRedirectContext { IsSafe = false })
                .AsException();

        return client;
    }

    private async ValueTask<IAuthorizationRequestObject?> LoadRequestObjectAsync(
        AuthorizationRequestObjectConfiguration configuration,
        IAuthorizationRequestMessage requestMessage,
        Client client,
        CancellationToken cancellationToken)
    {
        var requestJwt = requestMessage.RequestJwt;
        var requestUri = requestMessage.RequestUri;
        var errorFactory = requestMessage.OpenIdContext.ErrorFactory;

        RequestObjectSource requestObjectSource;
        string errorCode;

        if (requestUri is not null)
        {
            requestObjectSource = RequestObjectSource.Remote;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri;

            if (!string.IsNullOrEmpty(requestJwt))
                throw errorFactory
                    .InvalidRequest("Both the 'request' and 'request_uri' parameters cannot be present at the same time.", errorCode)
                    .AsException();

            if (!configuration.RequestUriEnabled)
                throw errorFactory
                    .RequestUriNotSupported()
                    .AsException();

            var requestUriMaxLength = configuration.RequestUriMaxLength;
            if (requestUri.OriginalString.Length > requestUriMaxLength)
                throw errorFactory
                    .InvalidRequest($"The 'request_uri' parameter must not exceed {requestUriMaxLength} characters.", errorCode)
                    .AsException();

            requestJwt = await FetchRequestUriAsync(
                configuration,
                errorFactory,
                client,
                requestUri,
                cancellationToken);
        }
        else if (!string.IsNullOrEmpty(requestJwt))
        {
            requestObjectSource = RequestObjectSource.Inline;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestJwt;

            if (!configuration.RequestJwtEnabled)
                throw errorFactory
                    .RequestJwtNotSupported()
                    .AsException();
        }
        else if (client.RequireRequestObject)
        {
            throw errorFactory
                .InvalidRequest("Client configuration requires the use of 'request' or 'request_uri' parameters.")
                .AsException();
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
                .ValidateAudience(configuration.Audience)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime();

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
        AuthorizationRequestObjectConfiguration configuration,
        IOpenIdErrorFactory errorFactory,
        Client client,
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
            var expectedContentType = configuration.ExpectedContentType;
            if (configuration.StrictContentType && contentType != expectedContentType)
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

        ValidateRequestMessage(requestMessage);

        if (requestObject != null)
            ValidateRequestObject(requestMessage, requestObject);

        ValidateRequest(client, authorizationRequest);

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
        IAuthorizationRequest request)
    {
        var errorFactory = request.OpenIdContext.ErrorFactory;

        var hasOpenIdScope = request.Scopes.Contains(OpenIdConstants.ScopeTypes.OpenId);
        var isImplicit = request.GrantType == GrantType.Implicit;
        var isHybrid = request.GrantType == GrantType.Hybrid;

        var hasCodeChallenge = !string.IsNullOrEmpty(request.CodeChallenge);
        var codeChallengeMethodIsPlain = request.CodeChallengeMethod == CodeChallengeMethod.Plain;

        // TODO: remove?
        if (!IsClientRedirectSafe(request.RedirectUri, client.AllowLoopback, client.RedirectUris))
            throw errorFactory.InvalidRequest($"The specified '{OpenIdConstants.Parameters.RedirectUri}' is not valid for this client application.").AsException();

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

        // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
        if (request.ResponseType.HasFlag(ResponseTypes.Token) && !client.AllowUnsafeTokenResponse)
            throw errorFactory.UnauthorizedClient("The client configuration prohibits the use of unsafe token responses.").AsException();

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (!hasCodeChallenge && client.RequirePkce)
            throw errorFactory.UnauthorizedClient("The client configuration requires the use of PKCE parameters.").AsException();

        if (hasCodeChallenge && codeChallengeMethodIsPlain && !client.AllowPlainCodeChallengeMethod)
            throw errorFactory.UnauthorizedClient("The client configuration prohibits the plain PKCE method.").AsException();

        // TODO: check allowed grant types from client configuration

        // TODO: check allowed scopes from client configuration

        // TODO: check allowed IdP from client configuration

        // TODO: add support for Resource Indicators
        // https://datatracker.ietf.org/doc/html/rfc8707

        // TODO: check session cookie
        // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Validation/Default/AuthorizeRequestValidator.cs#L801
    }

    #endregion

    #region AuthenticateCommand

    /// <inheritdoc />
    public async ValueTask<AuthenticateResult> HandleAsync(
        AuthenticateCommand command,
        CancellationToken cancellationToken)
    {
        var signInSchemeName = command.OpenIdContext.Tenant.Configuration.Authorization.SignInScheme;

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

        return await command.OpenIdContext.HttpContext.AuthenticateAsync(signInSchemeName);
    }

    #endregion

    #region AuthorizeCommand

    /// <inheritdoc />
    public async ValueTask<IOpenIdResult?> HandleAsync(
        AuthorizeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationContext = command.AuthorizationContext;
        var authenticationTicket = command.AuthenticationTicket;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var openIdContext = authorizationRequest.OpenIdContext;
        var errorFactory = openIdContext.ErrorFactory;
        var configuration = openIdContext.Tenant.Configuration;

        var promptType = authorizationRequest.PromptType;

        // TODO: check if supported
        if (promptType.HasFlag(PromptTypes.CreateAccount))
        {
            // TODO: redirect to create account page
            throw new NotImplementedException();
        }

        var reAuthenticate =
            promptType.HasFlag(PromptTypes.Login) ||
            promptType.HasFlag(PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            var returnUrl = await CallbackService.GetReturnUrlAsync(
                authorizationContext,
                "Client requested re-authentication.",
                cancellationToken);

            var redirectUrl = await LoginService.GetRedirectUrlAsync(
                authorizationContext,
                returnUrl,
                cancellationToken);

            return new OpenIdRedirectResult(redirectUrl);
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

            var returnUrl = await CallbackService.GetReturnUrlAsync(
                authorizationContext,
                "User not authenticated.",
                cancellationToken);

            var redirectUrl = await LoginService.GetRedirectUrlAsync(
                authorizationContext,
                returnUrl,
                cancellationToken);

            return new OpenIdRedirectResult(redirectUrl);
        }

        var subject = authenticationTicket.Principal.Identity as ClaimsIdentity ??
                      throw new InvalidOperationException("The AuthenticationTicket must contain a ClaimsIdentity.");

        if (!await ValidateUserIsActiveAsync(authorizationContext, authenticationTicket, cancellationToken))
        {
            var returnUrl = await CallbackService.GetReturnUrlAsync(
                authorizationContext,
                "User not active.",
                cancellationToken);

            var redirectUrl = await LoginService.GetRedirectUrlAsync(
                authorizationContext,
                returnUrl,
                cancellationToken);

            return new OpenIdRedirectResult(redirectUrl);
        }

        // TODO: check consent

        // TODO: check tenant

        // TODO: check IdP

        // check MaxAge
        if (!ValidateMaxAge(subject, authorizationRequest.MaxAge, configuration.ClockSkew))
        {
            var returnUrl = await CallbackService.GetReturnUrlAsync(
                authorizationContext,
                "MaxAge exceeded.",
                cancellationToken);

            var redirectUrl = await LoginService.GetRedirectUrlAsync(
                authorizationContext,
                returnUrl,
                cancellationToken);

            return new OpenIdRedirectResult(redirectUrl);
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

    #region Error Handling

    private static bool IsClientRedirectSafe(
        [NotNullWhen(true)] Uri? redirectUri,
        bool allowLoopback,
        ICollection<Uri> validRedirectUris
    ) =>
        redirectUri is not null &&
        (
            (allowLoopback && redirectUri.IsLoopback) ||
            validRedirectUris.Contains(redirectUri)
        );

    private async ValueTask<ClientRedirectContext> GetClientRedirectContextAsync(
        IOpenIdError openIdError,
        IAuthorizationSource authorizationSource,
        CancellationToken cancellationToken)
    {
        if (openIdError.ClientRedirectContext.HasValue)
            return openIdError.ClientRedirectContext.Value;

        var notSafeContext = new ClientRedirectContext { IsSafe = false };
        try
        {
            if (!authorizationSource.TryGetValue(OpenIdConstants.Parameters.ResponseMode, out var responseModeStringValues) || !Enum.TryParse(responseModeStringValues, out ResponseMode responseMode))
            {
                responseMode = ResponseMode.Query;
            }

            if (!authorizationSource.TryGetValue(OpenIdConstants.Parameters.RedirectUri, out var redirectUrl) || !Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri))
            {
                openIdError.ClientRedirectContext = notSafeContext;
                return notSafeContext;
            }

            if (!authorizationSource.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientId) || StringValues.IsNullOrEmpty(clientId))
            {
                openIdError.ClientRedirectContext = notSafeContext;
                return notSafeContext;
            }

            var client = await ClientStore.TryGetByClientIdAsync(
                authorizationSource.OpenIdContext.Tenant.TenantId,
                clientId.ToString(),
                cancellationToken);

            if (client == null)
            {
                openIdError.ClientRedirectContext = notSafeContext;
                return notSafeContext;
            }

            if (!IsClientRedirectSafe(redirectUri, client.AllowLoopback, client.RedirectUris))
            {
                openIdError.ClientRedirectContext = notSafeContext;
                return notSafeContext;
            }

            return new ClientRedirectContext
            {
                IsSafe = true,
                RedirectUri = redirectUri,
                ResponseMode = responseMode
            };
        }
        catch
        {
            openIdError.ClientRedirectContext = notSafeContext;
            return notSafeContext;
        }
    }

    #endregion
}
