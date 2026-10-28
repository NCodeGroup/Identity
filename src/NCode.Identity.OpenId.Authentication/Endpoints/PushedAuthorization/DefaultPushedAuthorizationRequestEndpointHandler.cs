#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization.Results;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization;

/// <summary>
/// Provides a default implementation of the pushed authorization request endpoint (OAuth 2.0 Pushed Authorization
/// Requests, RFC 9126). The endpoint is a thin orchestrator: it authenticates the client, loads and validates the
/// authorization request exactly as the authorization endpoint would, then persists it and returns the single-use
/// <c>request_uri</c>.
/// </summary>
internal class DefaultPushedAuthorizationRequestEndpointHandler(
    IClientAuthenticationService clientAuthenticationService,
    IPushedAuthorizationRequestService pushedAuthorizationRequestService,
    IAuthorizationResponseDeliveryResolver deliveryResolver
) : IEndpointProvider
{
    private IClientAuthenticationService ClientAuthenticationService { get; } =
        clientAuthenticationService;
    private IPushedAuthorizationRequestService PushedAuthorizationRequestService { get; } =
        pushedAuthorizationRequestService;
    private IAuthorizationResponseDeliveryResolver DeliveryResolver { get; } = deliveryResolver;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(OpenIdConstants.EndpointPaths.PushedAuthorizationRequest, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.PushedAuthorizationRequest)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces<PushedAuthorizationResult>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals(OpenIdConstants.ContentType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pushed authorization request endpoint (OAuth 2.0 Pushed Authorization Requests, RFC 9126).
    /// </summary>
    /// <remarks>
    /// Accepts the authorization request parameters directly from the client as application/x-www-form-urlencoded form
    /// data, authenticates the client, validates the request the same way the authorization endpoint would, then
    /// persists it and returns a single-use <c>request_uri</c> the client supplies to the authorization endpoint.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The request_uri and expires_in for the pushed authorization request.</response>
    /// <response code="400">An OAuth 2.0 error response carrying an error code and an optional error_description.</response>
    internal async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;
        var mediator = openIdContext.Mediator;

        var isPostVerb = httpContext.Request.Method == HttpMethods.Post;
        if (!isPostVerb || !IsApplicationFormContentType(httpContext))
        {
            return errorFactory
                .InvalidRequest(
                    $"Only POST requests with Content-Type '{OpenIdConstants.ContentType}' are supported."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken
        );

        if (authResult.IsError)
        {
            var error = authResult.Error;
            error.StatusCode ??= StatusCodes.Status400BadRequest;
            return error.AsHttpResult();
        }

        if (!authResult.HasClient)
        {
            return errorFactory
                .InvalidClient()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var openIdClient = authResult.Client;

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var requestValues = new RequestValuesUsingForm(formData);

        // RFC 9126 §2.1: the request_uri authorization request parameter must not be provided to the PAR endpoint.
        if (requestValues.ContainsKey(OpenIdProtocol.Parameters.RequestUri))
        {
            return errorFactory
                .InvalidRequest(
                    "The 'request_uri' parameter must not be used at the pushed authorization request endpoint."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        // Load and validate the authorization request the same way the authorization endpoint would (RFC 9126 §2.2).
        var authorizationRequest = await mediator.SendAsync<
            LoadAuthorizationRequestCommand,
            IAuthorizationRequest
        >(
            new LoadAuthorizationRequestCommand(openIdContext, openIdClient, requestValues),
            cancellationToken
        );

        // RFC 9126 §2.1: the pushed request is checked as the authorization endpoint would check it, including where its response would be delivered.
        var delivery = await DeliveryResolver.ResolveAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            cancellationToken
        );

        await mediator.SendAsync(
            new ValidateAuthorizationRequestCommand(
                openIdContext,
                openIdClient,
                authorizationRequest,
                delivery
            ),
            cancellationToken
        );

        var result = await PushedAuthorizationRequestService.StoreAsync(
            openIdContext,
            openIdClient,
            requestValues,
            cancellationToken
        );

        return TypedResults.Json(
            result,
            openIdEnvironment.JsonSerializerOptions,
            statusCode: StatusCodes.Status201Created
        );
    }
}
