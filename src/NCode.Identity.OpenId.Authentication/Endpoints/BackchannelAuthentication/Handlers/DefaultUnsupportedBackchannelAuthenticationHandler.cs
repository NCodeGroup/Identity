// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;

/// <summary>
/// Provides the default (capability-absent) implementation of the
/// <see cref="ICommandResponseHandler{AuthenticateBackchannelRequestCommand, AuthenticateSubjectDisposition}"/> seam
/// that reports the CIBA login hint as unresolvable. A deployment registers a login-hint resolution capability that
/// replaces this handler; without it a backchannel authentication request is rejected with <c>unknown_user_id</c>.
/// </summary>
internal class DefaultUnsupportedBackchannelAuthenticationHandler
    : ICommandResponseHandler<AuthenticateBackchannelRequestCommand, AuthenticateSubjectDisposition>
{
    /// <inheritdoc />
    public ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticateBackchannelRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = command.OpenIdContext.ErrorFactory;

        var error = errorFactory
            .Create(OpenIdProtocol.ErrorCodes.UnknownUserId)
            .WithDescription("The supplied login hint could not be resolved to a known user.")
            .WithStatusCode(StatusCodes.Status400BadRequest);

        return ValueTask.FromResult(new AuthenticateSubjectDisposition(error));
    }
}
