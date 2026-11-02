// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Provides the default handler for the <see cref="ParkEndSessionRequestCommand"/> message. It parks the request's
/// <c>id_token_hint</c>, <c>client_id</c>, <c>post_logout_redirect_uri</c> and <c>state</c>, with the session the logout
/// was asked for, as a ten-minute continuation that no client owns, because the request's <c>client_id</c> is not
/// authenticated.
/// </summary>
// See ADR-1380.
internal class DefaultParkEndSessionRequestHandler(IContinueService continueService)
    : ICommandResponseHandler<ParkEndSessionRequestCommand, string>
{
    private static TimeSpan ConfirmationLifetime { get; } = TimeSpan.FromMinutes(10);

    private IContinueService ContinueService { get; } = continueService;

    /// <inheritdoc />
    public async ValueTask<string> HandleAsync(
        ParkEndSessionRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var request = command.Request;
        var subject = command.Subject;
        var continuation = new EndSessionContinuation(
            request.IdTokenHint,
            request.ClientId,
            request.PostLogoutRedirectUri,
            request.State,
            subject.SessionId
        );

        return await ContinueService.GetContinueUrlAsync(
            command.OpenIdContext,
            OpenIdConstants.ContinueCodes.EndSession,
            clientId: null,
            subject.SubjectId,
            ConfirmationLifetime,
            continuation,
            cancellationToken
        );
    }
}
