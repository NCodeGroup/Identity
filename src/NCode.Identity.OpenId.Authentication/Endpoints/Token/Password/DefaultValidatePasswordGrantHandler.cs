// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="PasswordGrant"/>.
/// </summary>
internal class DefaultValidatePasswordGrantHandler
    : ICommandHandler<ValidateTokenGrantCommand<PasswordGrant>>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenGrantCommand<PasswordGrant> command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenRequest, passwordGrant) = command;
        var subjectAuthentication = passwordGrant.SubjectAuthentication;

        // see DefaultValidateTokenRequestHandler for additional validation

        // client_id/authenticated-client match is enforced upstream by DefaultClientAuthenticationService

        // validate the subject
        await TokenGrantSubjectValidation.ValidateAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            subjectAuthentication,
            cancellationToken
        );
    }
}
