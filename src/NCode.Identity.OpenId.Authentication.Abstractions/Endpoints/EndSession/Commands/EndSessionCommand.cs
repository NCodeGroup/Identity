// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Represents a mediator command that performs OpenID Connect RP-Initiated Logout for a loaded
/// <see cref="IEndSessionRequest"/>: it resolves the subject/session (via <see cref="ResolveEndSessionSubjectCommand"/>),
/// clears the OP login, dispatches <see cref="NCode.Identity.OpenId.Authentication.Sessions.OnSessionTerminatedCommand"/>
/// to run the logout side effects (grant/session revocation and back-channel notification), and resolves the validated
/// <c>post_logout_redirect_uri</c>. It returns an <see cref="EndSessionDisposition"/> describing where (if anywhere) the
/// user agent should be redirected.
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="Request">The loaded <see cref="IEndSessionRequest"/> to process.</param>
[PublicAPI]
public readonly record struct EndSessionCommand(
    OpenIdContext OpenIdContext,
    IEndSessionRequest Request
) : ICommand<EndSessionDisposition>;
