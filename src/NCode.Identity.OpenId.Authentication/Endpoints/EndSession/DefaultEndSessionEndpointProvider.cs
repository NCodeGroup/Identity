// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the <c>OpenID Connect</c>
/// RP-Initiated Logout <c>end_session_endpoint</c>
/// (<see href="https://openid.net/specs/openid-connect-rpinitiated-1_0.html">RP-Initiated Logout 1.0</see>). The
/// endpoint is a thin HTTP shell that dispatches the <see cref="EndSessionCommand"/> and either redirects to the
/// validated <c>post_logout_redirect_uri</c> or renders a logged-out confirmation. A logout that requires the
/// end-user's confirmation is parked through <see cref="ParkEndSessionRequestCommand"/> and the end-user is asked
/// through <see cref="EndSessionConfirmationChallengeCommand"/> instead.
/// </summary>
internal class DefaultEndSessionEndpointProvider : IEndpointProvider
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapMethods(
                OpenIdConstants.EndpointPaths.EndSession,
                [HttpMethods.Get, HttpMethods.Post],
                HandleRouteAsync
            )
            .WithName(OpenIdConstants.EndpointNames.EndSession)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status302Found)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable()
            .WithOpenIdFrontChannel();

    /// <summary>
    /// End session endpoint (OpenID Connect RP-Initiated Logout).
    /// </summary>
    /// <remarks>
    /// Logs the end-user out of the OpenID Provider: it clears the OP login, revokes the session and the subject's
    /// grants, notifies participating relying parties via back-channel logout, and redirects to the validated
    /// <c>post_logout_redirect_uri</c> when one is supplied. When an OP login is present and the request carries no valid
    /// <c>id_token_hint</c> for its session, it first parks the request and asks the end-user to confirm the logout.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The end-user has been logged out (no valid post-logout redirect was supplied).</response>
    /// <response code="302">The end-user has been logged out and is redirected to the validated post-logout URI, or is redirected to the page that asks them to confirm the logout.</response>
    internal virtual async ValueTask<IResult> HandleRouteAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var mediator = openIdContext.Mediator;

        var request = await mediator.SendAsync<LoadEndSessionRequestCommand, IEndSessionRequest>(
            new LoadEndSessionRequestCommand(openIdContext),
            cancellationToken
        );

        var subject = await mediator.SendAsync<ResolveEndSessionSubjectCommand, EndSessionSubject>(
            new ResolveEndSessionSubjectCommand(openIdContext, request),
            cancellationToken
        );

        // A logout that no valid id_token_hint vouches for is parked and confirmed by the end-user first
        // (RP-Initiated Logout 1.0 section 2). A challenge handler that leaves it unhandled opts out of the question.
        // See ADR-1380.
        if (subject.RequiresConfirmation)
        {
            var continueUrl = await mediator.SendAsync<ParkEndSessionRequestCommand, string>(
                new ParkEndSessionRequestCommand(openIdContext, request, subject),
                cancellationToken
            );

            var challenge = await mediator.SendAsync<
                EndSessionConfirmationChallengeCommand,
                ReadOnlyEndpointDisposition
            >(
                new EndSessionConfirmationChallengeCommand(openIdContext, request, continueUrl),
                cancellationToken
            );
            if (challenge.WasHandled)
            {
                return challenge.HasHttpResult ? challenge.HttpResult : EmptyHttpResult.Instance;
            }
        }

        var disposition = await mediator.SendAsync<EndSessionCommand, EndSessionDisposition>(
            new EndSessionCommand(openIdContext, request, subject),
            cancellationToken
        );

        return EndSessionHttpResults.Create(httpContext, disposition);
    }
}
