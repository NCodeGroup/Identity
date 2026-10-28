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
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization;

/// <summary>
/// Provides a default implementation of the device authorization endpoint (OAuth 2.0 Device Authorization Grant,
/// RFC 8628 §3.1–3.2). The endpoint is a thin orchestrator: it authenticates the client, loads the strongly-typed
/// <see cref="DeviceAuthorizationRequest"/>, then dispatches validation and request creation through mediator commands.
/// </summary>
internal class DefaultDeviceAuthorizationEndpointHandler(
    IClientAuthenticationService clientAuthenticationService
) : IEndpointProvider
{
    private IClientAuthenticationService ClientAuthenticationService { get; } =
        clientAuthenticationService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(OpenIdConstants.EndpointPaths.DeviceAuthorization, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.DeviceAuthorization)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces<DeviceAuthorizationResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals(
            OpenIdProtocol.MediaTypes.FormUrlEncoded,
            StringComparison.OrdinalIgnoreCase
        );

    /// <summary>
    /// Device authorization endpoint (OAuth 2.0 Device Authorization Grant, RFC 8628).
    /// </summary>
    /// <remarks>
    /// Issues a <c>device_code</c> and a human-typable <c>user_code</c> for an input-constrained client. The client
    /// authenticates and submits its requested scopes as application/x-www-form-urlencoded form data, then polls the
    /// token endpoint with the <c>device_code</c> while the end user approves the request at the verification endpoint.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The device_code, user_code, verification_uri, expires_in, and interval.</response>
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
                    $"Only POST requests with Content-Type '{OpenIdProtocol.MediaTypes.FormUrlEncoded}' are supported."
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
        var request = DeviceAuthorizationRequest.Load(openIdEnvironment, formData);

        await mediator.SendAsync(
            new ValidateDeviceAuthorizationRequestCommand(openIdContext, openIdClient, request),
            cancellationToken
        );

        var result = await mediator.SendAsync<
            CreateDeviceAuthorizationRequestCommand,
            DeviceAuthorizationResult
        >(
            new CreateDeviceAuthorizationRequestCommand(openIdContext, openIdClient, request),
            cancellationToken
        );

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }
}
