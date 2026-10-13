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
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Results;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;

/// <summary>
/// Provides a default implementation of the backchannel authentication endpoint (OpenID Connect CIBA §7–8). The
/// endpoint is a thin orchestrator: it authenticates the client, loads the strongly-typed
/// <see cref="BackchannelAuthenticationRequest"/>, then dispatches validation, subject resolution, and request creation
/// through mediator commands whose default handlers a capability package replaces.
/// </summary>
internal class DefaultBackchannelAuthenticationEndpointHandler(
    IClientAuthenticationService clientAuthenticationService
) : IEndpointProvider
{
    private IClientAuthenticationService ClientAuthenticationService { get; } =
        clientAuthenticationService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(OpenIdConstants.EndpointPaths.BackchannelAuthentication, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.BackchannelAuthentication)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces<BackchannelAuthenticationResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals(OpenIdConstants.ContentType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Backchannel authentication endpoint (OpenID Connect CIBA).
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The auth_req_id, expires_in, and interval.</response>
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
        var request = BackchannelAuthenticationRequest.Load(openIdEnvironment, formData);

        // A signed request object, when present, supersedes the form parameters (CIBA §7.1.1).
        var effectiveRequest = await mediator.SendAsync<
            LoadBackchannelAuthenticationRequestCommand,
            IBackchannelAuthenticationRequest
        >(
            new LoadBackchannelAuthenticationRequestCommand(openIdContext, openIdClient, request),
            cancellationToken
        );

        await mediator.SendAsync(
            new ValidateBackchannelAuthenticationRequestCommand(
                openIdContext,
                openIdClient,
                effectiveRequest
            ),
            cancellationToken
        );

        var disposition = await mediator.SendAsync<
            AuthenticateBackchannelRequestCommand,
            AuthenticateSubjectDisposition
        >(
            new AuthenticateBackchannelRequestCommand(
                openIdContext,
                openIdClient,
                effectiveRequest
            ),
            cancellationToken
        );

        if (disposition.HasError)
        {
            var error = disposition.Error;
            error.StatusCode ??= StatusCodes.Status400BadRequest;
            return error.AsHttpResult();
        }

        if (!disposition.IsAuthenticated)
        {
            return errorFactory
                .Create(OpenIdConstants.ErrorCodes.UnknownUserId)
                .WithDescription("The supplied login hint could not be resolved to a known user.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var result = await mediator.SendAsync<
            CreateBackchannelAuthenticationRequestCommand,
            BackchannelAuthenticationResult
        >(
            new CreateBackchannelAuthenticationRequestCommand(
                openIdContext,
                openIdClient,
                effectiveRequest,
                disposition.Ticket.Value
            ),
            cancellationToken
        );

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }
}
