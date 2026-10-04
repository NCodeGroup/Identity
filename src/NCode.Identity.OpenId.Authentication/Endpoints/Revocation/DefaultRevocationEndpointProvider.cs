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
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Revocation;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the <c>OAuth 2.0 Token Revocation</c>
/// endpoint (<see href="https://datatracker.ietf.org/doc/html/rfc7009">RFC 7009</see>). The endpoint is a thin HTTP
/// shell: it authenticates the client and parses the request, then delegates revocation to the mediator.
/// </summary>
internal class DefaultRevocationEndpointProvider(
    IClientAuthenticationService clientAuthenticationService
) : IEndpointProvider
{
    private IClientAuthenticationService ClientAuthenticationService { get; } =
        clientAuthenticationService;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(OpenIdConstants.EndpointPaths.Revocation, HandleRouteAsync)
            .WithName(OpenIdConstants.EndpointNames.Revocation)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .WithSummary("Revoke a token (RFC 7009)")
            .WithDescription(
                "Revokes a previously issued access token or refresh token. The client authenticates, then "
                    + "submits the token to invalidate as application/x-www-form-urlencoded form data. The response "
                    + "is always 200 OK, even when the token is unknown or already revoked, so that callers cannot "
                    + "probe token validity."
            )
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals(OpenIdConstants.ContentType, StringComparison.OrdinalIgnoreCase);

    internal virtual async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        var mediator = openIdContext.Mediator;
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;

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

        var formData = await httpContext.Request.ReadFormAsync(cancellationToken);
        var revocationRequest = TokenRevocationRequest.Load(openIdEnvironment, formData);

        await mediator.SendAsync(
            new RevokeTokenCommand(openIdContext, authResult.Client, revocationRequest),
            cancellationToken
        );

        // RFC 7009 section 2.2: respond 200 whether or not a token was revoked.
        return TypedResults.Ok();
    }
}
