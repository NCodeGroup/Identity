// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Handlers;

/// <summary>
/// Provides the default handler for the <see cref="EndSessionConfirmationChallengeCommand"/> message. It renders a
/// minimal page that asks the end-user to confirm the logout; the form posts to the one-time continue URL, whose
/// unguessable <c>state</c> is the CSRF secret, so every OpenID Provider asks even without an interactive surface. An
/// interactive surface replaces this handler with one that redirects the user-agent to its own confirmation page.
/// </summary>
internal class DefaultEndSessionConfirmationChallengeHandler
    : ICommandResponseHandler<EndSessionConfirmationChallengeCommand, ReadOnlyEndpointDisposition>
{
    /// <inheritdoc />
    public ValueTask<ReadOnlyEndpointDisposition> HandleAsync(
        EndSessionConfirmationChallengeCommand command,
        CancellationToken cancellationToken
    )
    {
        var action = HtmlEncoder.Default.Encode(command.ContinueUrl);
        var html =
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Sign out</title></head><body>"
            + $"<form method=\"post\" action=\"{action}\">"
            + "<h1>Sign out</h1><p>Do you want to sign out?</p>"
            + "<button id=\"logout-confirm\" type=\"submit\">Sign out</button>"
            + "</form></body></html>";

        // The resumed logout may redirect to the client, so the form may post to any origin.
        // See ADR-1380.
        command.OpenIdContext.Http.Response.SetHtmlPageSecurity(
            new HtmlPageSecurityPolicy { AllowsAnyFormAction = true }
        );

        var page = TypedResults.Content(html, "text/html; charset=utf-8", SecureEncoding.UTF8);
        var disposition = ReadOnlyEndpointDisposition.Handled(page);
        return ValueTask.FromResult(disposition);
    }
}
