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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Commands;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the authorization endpoint.
/// </summary>
public class DefaultAuthorizationEndpointHandler(
    ILogger<DefaultAuthorizationEndpointHandler> logger,
    IOpenIdContextFactory contextFactory,
    IClientAuthenticationService clientAuthenticationService,
    IAuthorizationEndpointLogic authorizationEndpointLogic
) : IOpenIdEndpointProvider
{
    private ILogger<DefaultAuthorizationEndpointHandler> Logger { get; } = logger;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;
    private IAuthorizationEndpointLogic AuthorizationEndpointLogic { get; } = authorizationEndpointLogic;

    /// <inheritdoc />
    public RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapMethods(
            OpenIdConstants.EndpointPaths.Authorization,
            [HttpMethods.Get, HttpMethods.Post],
            HandleRouteAsync
        )
        .WithName(OpenIdConstants.EndpointNames.Authorization)
        .WithOpenIdDiscoverable();

    private async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = await ContextFactory.CreateAsync(
            httpContext,
            mediator,
            cancellationToken
        );

        var errorFactory = openIdContext.ErrorFactory;

        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken
        );

        // for errors, before we redirect, we must validate the client_id and redirect_uri
        // otherwise we must return a failure HTTP status code

        if (!authResult.HasClient)
        {
            var error = authResult.Error ?? errorFactory.InvalidClient();
            error.StatusCode = StatusCodes.Status400BadRequest;
            return error.AsHttpResult();
        }

        // the following tries its best to not throw for OpenID protocol errors
        var requestValues = await mediator.SendAsync<LoadRequestValuesCommand, IRequestValues>(
            new LoadRequestValuesCommand(openIdContext),
            cancellationToken
        );

        // this will throw if client_id or redirect_uri are invalid
        var openIdClient = authResult.Client;
        var clientRedirectContext = GetClientRedirectContext(
            openIdContext,
            openIdClient,
            requestValues
        );

        // everything after this point is safe to redirect to the client

        var authorizationRequest = await mediator.SendAsync<LoadAuthorizationRequestCommand, IAuthorizationRequest>(
            new LoadAuthorizationRequestCommand(
                openIdContext,
                openIdClient,
                requestValues
            ),
            cancellationToken
        );

        var disposition = await AuthorizationEndpointLogic.ProcessRequestAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            clientRedirectContext,
            cancellationToken
        );

        if (disposition.HasHttpResult)
        {
            return disposition.HttpResult;
        }

        if (disposition.WasHandled)
        {
            return EmptyHttpResult.Instance;
        }

        Logger.LogError("The authorization request was not handled.");
        return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
    }

    private static ClientRedirectContext GetClientRedirectContext(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IRequestValues requestValues
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        var hasState = requestValues.TryGetValue(OpenIdConstants.Parameters.State, out var stateStringValues);
        var state = hasState && !StringValues.IsNullOrEmpty(stateStringValues) ? stateStringValues.ToString() : null;

        var hasResponseMode = requestValues.TryGetValue(OpenIdConstants.Parameters.ResponseMode, out var responseModeStringValues);
        string effectiveResponseMode;

        if (hasResponseMode)
        {
            if (responseModeStringValues.Count > 1)
            {
                throw errorFactory
                    .TooManyParameterValues(OpenIdConstants.Parameters.ResponseMode)
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .WithState(state)
                    .AsException();
            }

            effectiveResponseMode = responseModeStringValues.ToString();
            if (!settings.GetValue(SettingKeys.ResponseModesSupported).Contains(effectiveResponseMode))
            {
                throw errorFactory
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

        if (!requestValues.TryGetValue(OpenIdConstants.Parameters.RedirectUri, out var redirectUrl))
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

        var redirectUris = openIdClient.RedirectUris;
        var effectiveUri = redirectUri.GetComponents(
            UriComponents.Scheme |
            UriComponents.Host |
            UriComponents.Port |
            UriComponents.Path,
            UriFormat.UriEscaped);

        var allowLoopbackRedirect = settings.GetValue(SettingKeys.AllowLoopbackRedirect);
        var isSafe = (allowLoopbackRedirect && redirectUri.IsLoopback) || redirectUris.Contains(effectiveUri);
        if (!isSafe)
        {
            throw errorFactory
                .UnauthorizedClient("The specified 'redirect_uri' is not valid for the associated 'client_id'.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return new ClientRedirectContext(redirectUri, effectiveResponseMode, state);
    }
}
