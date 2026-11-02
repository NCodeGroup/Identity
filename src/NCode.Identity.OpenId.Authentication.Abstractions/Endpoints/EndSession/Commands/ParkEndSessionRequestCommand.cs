// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Represents a mediator command that parks an RP-Initiated Logout which requires the end-user's confirmation as a
/// one-time continuation bound to the session it was asked for, and returns the local URL that resumes it. The default
/// handler chooses the continuation's lifetime and payload; a deployment replaces it to change either.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="Request">The <see cref="IEndSessionRequest"/> to park.</param>
/// <param name="Subject">The resolved <see cref="EndSessionSubject"/> whose session the continuation is bound to.</param>
// See ADR-1380.
[PublicAPI]
public readonly record struct ParkEndSessionRequestCommand(
    OpenIdContext OpenIdContext,
    IEndSessionRequest Request,
    EndSessionSubject Subject
) : ICommand<string>;
