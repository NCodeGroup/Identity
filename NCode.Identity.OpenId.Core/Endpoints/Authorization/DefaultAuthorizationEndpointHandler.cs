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
using Microsoft.AspNetCore.Authentication;
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
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
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
    IContinueService continueService
) : IOpenIdEndpointProvider, IContinueProvider
{
    private ILogger<DefaultAuthorizationEndpointHandler> Logger { get; } = logger;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;
    private IContinueService ContinueService { get; } = continueService;

    /// <inheritdoc />
    public RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapMethods(
            OpenIdConstants.EndpointPaths.Authorization,
            [HttpMethods.Get, HttpMethods.Post],
            HandleRouteAsync)
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

        try
        {
            var authorizationRequest = await mediator.SendAsync<LoadAuthorizationRequestCommand, IAuthorizationRequest>(
                new LoadAuthorizationRequestCommand(
                    openIdContext,
                    openIdClient,
                    requestValues
                ),
                cancellationToken
            );

            var disposition = await ProcessAuthorizationRequestAsync(
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

            Logger.LogWarning("The authorization request was not handled.");
            return TypedResults.BadRequest();
        }
        catch (HttpResultException exception)
        {
            return exception.HttpResult;
        }
        catch (Exception exception)
        {
            var (redirectUri, responseMode, state) = clientRedirectContext;
            var openIdError = HandleException(errorFactory, exception, state);
            return new AuthorizationResult(redirectUri, responseMode, openIdError);
        }
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

    private async ValueTask<EndpointDisposition> ChallengeAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        CancellationToken cancellationToken
    )
    {
        var mediator = openIdContext.Mediator;
        var clientSettings = openIdClient.Settings;

        var continueUrl = await ContinueService.GetContinueUrlAsync(
            openIdContext,
            OpenIdConstants.ContinueCodes.Authorization,
            openIdClient.ClientId,
            subjectId: null,
            clientSettings.GetValue(SettingKeys.ContinueAuthorizationLifetime),
            authorizationRequest,
            cancellationToken
        );

        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = continueUrl
        };

        authenticationProperties.SetString(
            OpenIdConstants.AuthenticationPropertyItems.TenantId,
            openIdContext.Tenant.TenantId
        );

        // for the following parameters, see the OpenIdConnectHandler class for reference:
        // https://github.com/dotnet/aspnetcore/blob/cd5ca6985645aa4929747bd690109a99a97126e3/src/Security/Authentication/OpenIdConnect/src/OpenIdConnectHandler.cs#L398

        authenticationProperties.SetParameter(
            OpenIdConstants.Parameters.Prompt,
            // OpenIdConnectHandler requires a string, but we have an IReadOnlyList<string>
            string.Join(OpenIdConstants.ParameterSeparatorChar, authorizationRequest.PromptTypes)
        );

        authenticationProperties.SetParameter(
            OpenIdConstants.Parameters.Scope,
            // OpenIdConnectHandler requires an ICollection<string> but we have an IReadOnlyList<string>
            authorizationRequest.Scopes.ToList()
        );

        authenticationProperties.SetParameter(
            OpenIdConstants.Parameters.MaxAge,
            // OpenIdConnectHandler uses TimeSpan? just like we do
            authorizationRequest.MaxAge
        );

        // additional authentication properties can be set via mediator middleware

        var disposition = await mediator.SendAsync<ChallengeSubjectCommand, EndpointDisposition>(
            new ChallengeSubjectCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationProperties
            ),
            cancellationToken
        );
        return disposition;
    }

    private async ValueTask<EndpointDisposition> ProcessAuthorizationRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        ClientRedirectContext clientRedirectContext,
        CancellationToken cancellationToken
    )
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
                authorizationRequest
            ),
            cancellationToken
        );

        var authenticateSubjectDisposition = await mediator.SendAsync<AuthenticateSubjectCommand, AuthenticateSubjectDisposition>(
            new AuthenticateSubjectCommand(
                openIdContext,
                openIdClient,
                authorizationRequest
            ),
            cancellationToken
        );

        if (authenticateSubjectDisposition.HasError)
        {
            return EndpointDisposition.Handled(
                new AuthorizationResult(
                    redirectUri,
                    effectiveResponseMode,
                    authenticateSubjectDisposition.Error
                )
            );
        }

        if (!authenticateSubjectDisposition.IsAuthenticated)
        {
            return await ChallengeAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                cancellationToken
            );
        }

        var authenticationTicket = authenticateSubjectDisposition.Ticket.Value;

        var authorizeSubjectDisposition = await mediator.SendAsync<AuthorizeSubjectCommand, AuthorizeSubjectDisposition>(
            new AuthorizeSubjectCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket
            ),
            cancellationToken
        );

        if (authorizeSubjectDisposition.HasError)
        {
            return EndpointDisposition.Handled(
                new AuthorizationResult(
                    redirectUri,
                    effectiveResponseMode,
                    authorizeSubjectDisposition.Error
                )
            );
        }

        if (authorizeSubjectDisposition.ChallengeRequired)
        {
            return await ChallengeAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                cancellationToken
            );
        }

        var authorizationTicket = await mediator.SendAsync<CreateAuthorizationTicketCommand, IAuthorizationTicket>(
            new CreateAuthorizationTicketCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                authenticationTicket
            ),
            cancellationToken
        );

        return EndpointDisposition.Handled(
            new AuthorizationResult(
                redirectUri,
                effectiveResponseMode,
                authorizationTicket
            )
        );
    }

    #region IContinueProvider

    /// <inheritdoc />
    public string ContinueCode => OpenIdConstants.ContinueCodes.Authorization;

    /// <inheritdoc />
    public async ValueTask<EndpointDisposition> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;

        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken);

        if (!authResult.HasClient)
        {
            var error = authResult.Error ?? errorFactory.InvalidClient();
            error.StatusCode = StatusCodes.Status400BadRequest;
            return EndpointDisposition.Handled(error.AsHttpResult());
        }

        var openIdClient = authResult.Client;
        var authorizationRequest = continuePayloadJson.Deserialize<IAuthorizationRequest>(openIdEnvironment.JsonSerializerOptions);
        if (authorizationRequest == null)
            throw new InvalidOperationException("JSON deserialization returned null.");

        authorizationRequest.IsContinuation = true;

        var clientRedirectContext = new ClientRedirectContext(
            authorizationRequest.RedirectUri,
            authorizationRequest.ResponseMode,
            authorizationRequest.State
        );

        try
        {
            var disposition = await ProcessAuthorizationRequestAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                clientRedirectContext,
                cancellationToken
            );
            return disposition;
        }
        catch (HttpResultException exception)
        {
            return EndpointDisposition.Handled(exception.HttpResult);
        }
        catch (Exception exception)
        {
            var (redirectUri, responseMode, state) = clientRedirectContext;
            var openIdError = HandleException(errorFactory, exception, state);
            var httpResult = new AuthorizationResult(redirectUri, responseMode, openIdError);
            return EndpointDisposition.Handled(httpResult);
        }
    }

    #endregion

    private IOpenIdError HandleException(IOpenIdErrorFactory errorFactory, Exception exception, string? state) =>
        exception switch
        {
            OpenIdException openIdException => openIdException.Error
                .WithState(state),

            _ => errorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithState(state)
                .WithException(exception)
        };
}
