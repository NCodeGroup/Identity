// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Represents a mediator command to ask the end-user to confirm an RP-Initiated Logout parked at
/// <see cref="ContinueUrl"/>. The default handler renders a minimal page whose form posts to the continue URL, an
/// interactive surface redirects to its own confirmation page, and a handler that leaves the command unhandled lets
/// the logout proceed without confirmation.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
/// <param name="Request">The <see cref="IEndSessionRequest"/> that requires confirmation.</param>
/// <param name="ContinueUrl">The local URL that resumes the logout once the end-user confirms it.</param>
[PublicAPI]
public readonly record struct EndSessionConfirmationChallengeCommand(
    OpenIdContext OpenIdContext,
    IEndSessionRequest Request,
    string ContinueUrl
) : ICommand<ReadOnlyEndpointDisposition>;
