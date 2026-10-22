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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.Messages.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the authorization endpoint. The
/// authorization request travels through the user agent, so the endpoint identifies the client by its <c>client_id</c>
/// and never authenticates it; a rule that depends on the client type reads the type the client is registered as.
/// </summary>
internal class DefaultAuthorizationEndpointHandler(
    ILogger<DefaultAuthorizationEndpointHandler> logger,
    IOpenIdClientResolver clientResolver,
    IAuthorizationRequestProcessor authorizationRequestProcessor,
    IPushedAuthorizationRequestService pushedAuthorizationRequestService,
    IKnownParameterCollectionProvider knownParameterCollectionProvider,
    IEnumerable<IAuthorizationResponseModeHandler> responseModeHandlers
) : IEndpointProvider
{
    private FrozenSet<string> ServedResponseModes { get; } =
        responseModeHandlers
            .Select(handler => handler.ResponseMode)
            .ToFrozenSet(StringComparer.Ordinal);

    private ILogger<DefaultAuthorizationEndpointHandler> Logger { get; } = logger;
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;
    private IAuthorizationRequestProcessor AuthorizationRequestProcessor { get; } =
        authorizationRequestProcessor;
    private IPushedAuthorizationRequestService PushedAuthorizationRequestService { get; } =
        pushedAuthorizationRequestService;
    private IKnownParameterCollectionProvider KnownParameterCollectionProvider { get; } =
        knownParameterCollectionProvider;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapMethods(
                OpenIdConstants.EndpointPaths.Authorization,
                [HttpMethods.Get, HttpMethods.Post],
                HandleRouteAsync
            )
            .WithName(OpenIdConstants.EndpointNames.Authorization)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .WithOpenIdFormParameters(
                KnownParameterCollectionProvider,
                OpenIdConstants.EndpointNames.Authorization
            )
            .WithOpenIdDiscoverable()
            .WithOpenIdFrontChannel();
    }

    /// <summary>
    /// Authorization endpoint (OAuth 2.0 / OpenID Connect).
    /// </summary>
    /// <remarks>
    /// Begins an OAuth 2.0 / OpenID Connect authorization request. Parameters such as response_type, client_id,
    /// redirect_uri, scope, and state are supplied as query-string values (GET) or form values (POST).
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The authorization response. Depending on the requested response_mode, the user-agent is redirected to the client's redirect_uri with the result, or an auto-submitting form is returned.</response>
    internal async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        var mediator = openIdContext.Mediator;
        var errorFactory = openIdContext.ErrorFactory;

        // the following tries its best to not throw for OpenID protocol errors
        var requestValues = await mediator.SendAsync<LoadRequestValuesCommand, IRequestValues>(
            new LoadRequestValuesCommand(openIdContext),
            cancellationToken
        );

        // for errors, before we redirect, we must validate the client_id and redirect_uri
        // otherwise we must return a failure HTTP status code

        if (!TryGetClientId(openIdContext, requestValues, out var clientId, out var clientIdError))
        {
            return clientIdError.AsHttpResult();
        }

        var openIdClient = await ClientResolver.ResolveClientOrDefaultAsync(
            openIdContext,
            clientId,
            cancellationToken
        );
        if (openIdClient is null)
        {
            return errorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        // RFC 9126: if the request references a pushed authorization request, replay its stored parameters; otherwise
        // enforce the require_pushed_authorization_requests policy. This runs before any parameter is trusted.
        requestValues = await ResolvePushedAuthorizationRequestAsync(
            openIdContext,
            openIdClient,
            requestValues,
            cancellationToken
        );

        ClientRedirectContext clientRedirectContext;
        IAuthorizationRequest authorizationRequest;

        if (CarriesRedirectTargetOnlyInRequestObject(requestValues))
        {
            // RFC 9126 §3 / RFC 9101 §5: a request object may be the only carrier of the authorization parameters, so
            // the redirect target is known only once the object is verified; an error until then is not redirected.
            authorizationRequest = await mediator.SendAsync<
                LoadAuthorizationRequestCommand,
                IAuthorizationRequest
            >(
                new LoadAuthorizationRequestCommand(openIdContext, openIdClient, requestValues),
                cancellationToken
            );

            // this will throw if redirect_uri is invalid
            clientRedirectContext = GetClientRedirectContext(
                openIdContext,
                openIdClient,
                GetRequestObjectValues(openIdContext, authorizationRequest, requestValues)
            );
        }
        else
        {
            // this will throw if client_id or redirect_uri are invalid
            clientRedirectContext = GetClientRedirectContext(
                openIdContext,
                openIdClient,
                requestValues
            );

            // everything after this point is safe to redirect to the client

            authorizationRequest = await mediator.SendAsync<
                LoadAuthorizationRequestCommand,
                IAuthorizationRequest
            >(
                new LoadAuthorizationRequestCommand(openIdContext, openIdClient, requestValues),
                cancellationToken
            );

            // RFC 9101 §6.3: once the request object is verified, only its values are used, so every later response,
            // an error included, carries the object's state and response mode rather than the unsigned outer ones.
            // The redirect target stays the one validated above; validation refuses an object whose redirect_uri
            // differs from it, and that refusal is delivered there.
            if (authorizationRequest.OriginalRequestObject is not null)
            {
                var requestObjectValues = GetRequestObjectValues(
                    openIdContext,
                    authorizationRequest,
                    requestValues
                );
                var state = GetState(requestObjectValues);
                clientRedirectContext = clientRedirectContext with
                {
                    ResponseMode = GetEffectiveResponseMode(
                        openIdContext,
                        openIdClient,
                        requestObjectValues,
                        state
                    ),
                    State = state,
                };
            }
        }

        var disposition = await AuthorizationRequestProcessor.ProcessRequestAsync(
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

        Logger.AuthorizationRequestNotHandled();
        return TypedResults.StatusCode(StatusCodes.Status501NotImplemented);
    }

    private async ValueTask<IRequestValues> ResolvePushedAuthorizationRequestAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IRequestValues requestValues,
        CancellationToken cancellationToken
    )
    {
        if (
            requestValues.TryGetValue(
                OpenIdConstants.Parameters.RequestUri,
                out var requestUriValues
            )
            && requestUriValues.Count == 1
            && requestUriValues[0] is { } requestUri
            && requestUri.StartsWith(
                OpenIdConstants.PushedAuthorizationRequestUriPrefix,
                StringComparison.Ordinal
            )
        )
        {
            return await PushedAuthorizationRequestService.ConsumeAsync(
                openIdContext,
                openIdClient,
                requestUri,
                cancellationToken
            );
        }

        if (openIdClient.Settings.GetValue(OpenIdSettingKeys.RequirePushedAuthorizationRequests))
        {
            throw openIdContext
                .ErrorFactory.InvalidRequest(
                    "This client requires the use of pushed authorization requests."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        return requestValues;
    }

    // Identifies the client the request names. The request travels through the user agent, so it identifies the client
    // without authenticating it, and a client secret in it is refused: RFC 6749 §2.3.1 confines client credentials to
    // the body of a back-channel request.
    private static bool TryGetClientId(
        OpenIdContext openIdContext,
        IRequestValues requestValues,
        [NotNullWhen(true)] out string? clientId,
        [NotNullWhen(false)] out IOpenIdError? error
    )
    {
        var errorFactory = openIdContext.ErrorFactory;

        if (requestValues.ContainsKey(OpenIdConstants.Parameters.ClientSecret))
        {
            clientId = null;
            error = errorFactory
                .InvalidRequest("The client secret must not be sent to the authorization endpoint.")
                .WithStatusCode(StatusCodes.Status400BadRequest);
            return false;
        }

        if (
            !requestValues.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientIdValues)
            || clientIdValues.Count != 1
            || clientIdValues[0] is not { Length: > 0 } value
        )
        {
            clientId = null;
            error = errorFactory.InvalidClient().WithStatusCode(StatusCodes.Status400BadRequest);
            return false;
        }

        clientId = value;
        error = null;
        return true;
    }

    private static bool CarriesRedirectTargetOnlyInRequestObject(IRequestValues requestValues) =>
        !requestValues.ContainsKey(OpenIdConstants.Parameters.RedirectUri)
        && (
            requestValues.ContainsKey(OpenIdConstants.Parameters.Request)
            || requestValues.ContainsKey(OpenIdConstants.Parameters.RequestUri)
        );

    // Only the verified request object's values are used (RFC 9101 §6.3); response_type alone may still come from the
    // outer request, matching IAuthorizationRequest.ResponseTypes.
    private static RequestValuesUsingCollection GetRequestObjectValues(
        OpenIdContext openIdContext,
        IAuthorizationRequest authorizationRequest,
        IRequestValues requestValues
    )
    {
        var openIdEnvironment = openIdContext.Environment;
        var values = new Dictionary<string, StringValues>(StringComparer.Ordinal);

        if (authorizationRequest.OriginalRequestObject is { } requestObject)
        {
            foreach (var parameter in requestObject.Parameters)
            {
                values[parameter.Descriptor.ParameterName] = parameter.GetStringValues(
                    openIdEnvironment
                );
            }
        }

        if (
            !values.ContainsKey(OpenIdConstants.Parameters.ResponseType)
            && requestValues.TryGetValue(
                OpenIdConstants.Parameters.ResponseType,
                out var responseTypeStringValues
            )
        )
        {
            values[OpenIdConstants.Parameters.ResponseType] = responseTypeStringValues;
        }

        return new RequestValuesUsingCollection(RequestValuesSourceTypes.RequestObject, values);
    }

    private ClientRedirectContext GetClientRedirectContext(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IRequestValues requestValues
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        var state = GetState(requestValues);
        var effectiveResponseMode = GetEffectiveResponseMode(
            openIdContext,
            openIdClient,
            requestValues,
            state
        );

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
            UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path,
            UriFormat.UriEscaped
        );

        var allowLoopbackRedirect = settings.GetValue(OpenIdSettingKeys.AllowLoopbackRedirect);
        var isSafe =
            (allowLoopbackRedirect && redirectUri.IsLoopback)
            || redirectUris.Contains(effectiveUri);
        if (!isSafe)
        {
            throw errorFactory
                .UnauthorizedClient(
                    "The specified 'redirect_uri' is not valid for the associated 'client_id'."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return new ClientRedirectContext(redirectUri, effectiveResponseMode, state);
    }

    private static string? GetState(IRequestValues requestValues) =>
        requestValues.TryGetValue(OpenIdConstants.Parameters.State, out var stateStringValues)
        && !StringValues.IsNullOrEmpty(stateStringValues)
            ? stateStringValues.ToString()
            : null;

    private string GetEffectiveResponseMode(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IRequestValues requestValues,
        string? state
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        var hasResponseMode = requestValues.TryGetValue(
            OpenIdConstants.Parameters.ResponseMode,
            out var responseModeStringValues
        );
        // The default follows the response type, so a response that returns a token is never redirected in a query
        // string, whether it carries the tokens or an error (ADR-0141); the JARM 'jwt' shorthand resolves the same way.
        requestValues.TryGetValue(
            OpenIdConstants.Parameters.ResponseType,
            out var responseTypeStringValues
        );
        var responseTypeNames = SplitResponseTypeNames(responseTypeStringValues);
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

            effectiveResponseMode = ResponseTypeValues.ResolveResponseMode(
                responseModeStringValues.ToString(),
                responseTypeNames
            );
            // A response mode no registered handler serves could not carry even an error back to the client.
            if (
                !ServedResponseModes.Contains(effectiveResponseMode)
                || (
                    settings.TryGetValue(
                        OpenIdSettingKeys.ResponseModesSupported,
                        out var responseModesSupported
                    ) && !responseModesSupported.Contains(effectiveResponseMode)
                )
            )
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
            effectiveResponseMode = ResponseTypeValues.GetDefaultResponseMode(responseTypeNames);
        }

        return effectiveResponseMode;
    }

    private static List<string> SplitResponseTypeNames(StringValues responseTypeStringValues) =>
        [
            .. responseTypeStringValues.SelectMany(value =>
                value?.Split(OpenIdConstants.ParameterSeparatorChar) ?? []
            ),
        ];
}
