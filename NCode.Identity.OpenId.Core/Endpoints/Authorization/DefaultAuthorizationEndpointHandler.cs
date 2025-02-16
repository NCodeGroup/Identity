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

using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the authorization endpoint.
/// </summary>
public class DefaultAuthorizationEndpointHandler(
    OpenIdEnvironment openIdEnvironment,
    IOpenIdErrorFactory errorFactory,
    IOpenIdContextFactory contextFactory,
    IClientAuthenticationService clientAuthenticationService
) : IOpenIdEndpointProvider, IContinueProvider
{
    private OpenIdEnvironment OpenIdEnvironment { get; } = openIdEnvironment;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapMethods(
            OpenIdConstants.EndpointPaths.Authorization,
            [HttpMethods.Get, HttpMethods.Post],
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

        if (!authResult.HasClient)
        {
            var error = authResult.Error ?? ErrorFactory.InvalidClient();
            error.StatusCode = StatusCodes.Status400BadRequest;
            return error.AsResult();
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

    private ClientRedirectContext GetClientRedirectContext(OpenIdClient openIdClient, IAuthorizationSource authorizationSource)
    {
        var settings = openIdClient.Settings;

        var hasState = authorizationSource.RequestValues.TryGetValue(OpenIdConstants.Parameters.State, out var stateStringValues);
        var state = hasState && !StringValues.IsNullOrEmpty(stateStringValues) ? stateStringValues.ToString() : null;

        var hasResponseMode = authorizationSource.RequestValues.TryGetValue(OpenIdConstants.Parameters.ResponseMode, out var responseModeStringValues);
        string effectiveResponseMode;

        if (hasResponseMode)
        {
            if (responseModeStringValues.Count > 1)
            {
                throw ErrorFactory
                    .TooManyParameterValues(OpenIdConstants.Parameters.ResponseMode)
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .WithState(state)
                    .AsException();
            }

            effectiveResponseMode = responseModeStringValues.ToString();
            if (!settings.ResponseModesSupported.Contains(effectiveResponseMode))
            {
                throw ErrorFactory
                    .InvalidParameterValue(OpenIdConstants.Parameters.ResponseMode)
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .WithState(state)
                    .AsException();
            }
        }
        else
        {
            effectiveResponseMode = OpenIdConstants.ResponseModes.Query;
        }

        if (!authorizationSource.RequestValues.TryGetValue(OpenIdConstants.Parameters.RedirectUri, out var redirectUrl))
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

        var redirectUrls = openIdClient.RedirectUrls;
        var effectiveUrl = redirectUri.GetComponents(
            UriComponents.Scheme |
            UriComponents.Host |
            UriComponents.Port |
            UriComponents.Path,
            UriFormat.UriEscaped);

        var isSafe = (settings.AllowLoopbackRedirect && redirectUri.IsLoopback) || redirectUrls.Contains(effectiveUrl);
        if (!isSafe)
        {
            throw ErrorFactory
                .UnauthorizedClient("The specified 'redirect_uri' is not valid for the associated 'client_id'.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return new ClientRedirectContext(redirectUri, effectiveResponseMode, state);
    }

    private async ValueTask<IResult> ProcessAuthorizationRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        ClientRedirectContext clientRedirectContext,
        CancellationToken cancellationToken)
    {
        var mediator = openIdContext.Mediator;

        var redirectUri = clientRedirectContext.RedirectUri;

        // the request object may have changed the response mode
        var requestObject = authorizationRequest.OriginalRequestObject;
        var effectiveResponseMode = !string.IsNullOrEmpty(requestObject?.ResponseMode) ?
            requestObject.ResponseMode :
            clientRedirectContext.ResponseMode;

        await mediator.SendAsync(
            new ValidateAuthorizationRequestCommand(
                openIdContext,
                openIdClient,
                authorizationRequest),
            cancellationToken);

        var authenticateResult = await mediator.SendAsync<AuthenticateCommand, AuthenticateSubjectResult>(
            new AuthenticateCommand(
                openIdContext,
                openIdClient,
                authorizationRequest),
            cancellationToken);

        if (!authenticateResult.IsSuccess)
        {
            var error = authenticateResult.Error ?? ErrorFactory.AccessDenied("Unable to authenticate the end-user.");
            error.State = clientRedirectContext.State;
            return new AuthorizationResult(
                redirectUri,
                effectiveResponseMode,
                error);
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

        return new AuthorizationResult(redirectUri, effectiveResponseMode, authorizationTicket);
    }

    #region IContinueProvider

    /// <inheritdoc />
    public string ContinueCode => OpenIdConstants.ContinueCodes.Authorization;

    /// <inheritdoc />
    public async ValueTask<IResult> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        CancellationToken cancellationToken)
    {
        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken);

        if (!authResult.HasClient)
        {
            var error = authResult.Error ?? ErrorFactory.InvalidClient();
            error.StatusCode = StatusCodes.Status400BadRequest;
            return error.AsResult();
        }

        var openIdClient = authResult.Client;
        var authorizationRequest = continuePayloadJson.Deserialize<IAuthorizationRequest>(OpenIdEnvironment.JsonSerializerOptions);
        if (authorizationRequest == null)
            throw new InvalidOperationException("JSON deserialization returned null.");

        authorizationRequest.IsContinuation = true;

        // prevent infinite loops from continuations and user interaction
        authorizationRequest.OriginalRequestMessage.PromptTypes = [];

        var clientRedirectContext = new ClientRedirectContext(
            authorizationRequest.RedirectUri,
            authorizationRequest.ResponseMode,
            authorizationRequest.State
        );

        var result = await ProcessAuthorizationRequestAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            clientRedirectContext,
            cancellationToken);

        return result;
    }

    #endregion
}
