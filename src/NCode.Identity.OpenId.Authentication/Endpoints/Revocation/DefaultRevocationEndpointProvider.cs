// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using NCode.AspNetCore;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Revocation.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
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
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

    private static bool IsApplicationFormContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals(
            OpenIdProtocol.MediaTypes.FormUrlEncoded,
            StringComparison.OrdinalIgnoreCase
        );

    /// <summary>
    /// Revoke a token (RFC 7009).
    /// </summary>
    /// <remarks>
    /// Revokes a previously issued access or refresh token. The client authenticates, then submits the token to
    /// invalidate as application/x-www-form-urlencoded form data.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The request was processed. Returned even for an unknown or already-revoked token, so callers cannot probe token validity.</response>
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
                    $"Only POST requests with Content-Type '{OpenIdProtocol.MediaTypes.FormUrlEncoded}' are supported."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        // The form is read before client authentication, so an unreadable body is the client's error (400).
        var formData = await httpContext.Request.TryReadFormAsync(cancellationToken);
        if (formData is null)
            return UnreadableForm.CreateError(errorFactory).AsHttpResult();

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

        var revocationRequest = TokenRevocationRequest.Load(openIdEnvironment, formData);

        await mediator.SendAsync(
            new RevokeTokenCommand(openIdContext, authResult.Client, revocationRequest),
            cancellationToken
        );

        // RFC 7009 section 2.2: respond 200 whether or not a token was revoked.
        return TypedResults.Ok();
    }
}
