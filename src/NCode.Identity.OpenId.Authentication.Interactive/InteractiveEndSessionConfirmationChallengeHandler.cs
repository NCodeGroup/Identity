// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Handles the <see cref="EndSessionConfirmationChallengeCommand"/> for the interactive surface by redirecting the
/// user-agent to the logout confirmation page, carrying the parked continuation as the return URL so the logout
/// resumes once the end-user confirms it, and the request's <c>ui_locales</c> so the page renders in the end-user's
/// preferred language (RP-Initiated Logout 1.0 §2). This replaces the core default handler when the interactive
/// surface is registered.
/// </summary>
internal sealed class InteractiveEndSessionConfirmationChallengeHandler(
    InteractiveSurfacePaths surfacePaths
) : ICommandResponseHandler<EndSessionConfirmationChallengeCommand, ReadOnlyEndpointDisposition>
{
    private InteractiveSurfacePaths SurfacePaths { get; } = surfacePaths;

    /// <inheritdoc />
    public ValueTask<ReadOnlyEndpointDisposition> HandleAsync(
        EndSessionConfirmationChallengeCommand command,
        CancellationToken cancellationToken
    )
    {
        // See ADR-1380.
        var confirmPath = SurfacePaths.GetSurfacePath(
            command.OpenIdContext.Http,
            InteractiveAuthenticationDefaults.LogoutConfirmPath
        );
        var location = InteractiveLocaleSelector.AppendUiLocalesQuery(
            $"{confirmPath}?ReturnUrl=" + Uri.EscapeDataString(command.ContinueUrl),
            command.Request.UiLocales
        );

        RedirectHttpResult result = TypedResults.Redirect(location);
        var disposition = ReadOnlyEndpointDisposition.Handled(result);
        return ValueTask.FromResult(disposition);
    }
}
