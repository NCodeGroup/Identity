// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Represents a mediator command that resolves the <see cref="EndSessionSubject"/> (subject, session, requesting
/// client, and participating clients) for an RP-Initiated Logout from the request's <c>id_token_hint</c>, falling back
/// to the current OP login cookie.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="Request">The <see cref="IEndSessionRequest"/> whose <c>id_token_hint</c> identifies the subject.</param>
[PublicAPI]
public readonly record struct ResolveEndSessionSubjectCommand(
    OpenIdContext OpenIdContext,
    IEndSessionRequest Request
) : ICommand<EndSessionSubject>;
