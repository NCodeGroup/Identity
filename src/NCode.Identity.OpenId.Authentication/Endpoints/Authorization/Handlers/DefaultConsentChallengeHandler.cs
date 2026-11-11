// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides the default (no-op) handler for the <see cref="ConsentChallengeCommand"/> message. Without an interactive
/// consent surface there is nothing to redirect to, so the command is left unhandled and the authorization endpoint
/// degrades to an <c>interaction_required</c> error. An interactive surface (for example the opt-in login/consent
/// module) replaces this handler with one that redirects the user-agent to its consent page.
/// </summary>
internal class DefaultConsentChallengeHandler
    : ICommandResponseHandler<ConsentChallengeCommand, ReadOnlyEndpointDisposition>
{
    /// <inheritdoc />
    public ValueTask<ReadOnlyEndpointDisposition> HandleAsync(
        ConsentChallengeCommand command,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(ReadOnlyEndpointDisposition.UnHandled());
}
