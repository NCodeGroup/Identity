// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;

/// <summary>
/// Provides the default (capability-absent) implementation of the
/// <see cref="ICommandResponseHandler{AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition}"/> seam that
/// reports the resource-owner password grant as unsupported. The grant is enabled by registering the local-account
/// authentication capability (the <c>NCode.Identity.OpenId.Accounts.Authentication</c> package), which replaces this
/// handler; without that capability a password grant request is rejected with <c>unsupported_grant_type</c>.
/// </summary>
internal class DefaultUnsupportedPasswordGrantHandler(
    ILogger<DefaultUnsupportedPasswordGrantHandler> logger
) : ICommandResponseHandler<AuthenticatePasswordGrantCommand, AuthenticateSubjectDisposition>
{
    private ILogger<DefaultUnsupportedPasswordGrantHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public ValueTask<AuthenticateSubjectDisposition> HandleAsync(
        AuthenticatePasswordGrantCommand command,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = command.OpenIdContext.ErrorFactory;

        Logger.PasswordGrantNotSupported();

        var error = errorFactory.UnsupportedGrantType(
            "The resource owner password credential grant type is not supported."
        );

        return ValueTask.FromResult(new AuthenticateSubjectDisposition(error));
    }
}
