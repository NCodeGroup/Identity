// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="CibaGrant"/>.
/// </summary>
internal class DefaultValidateCibaGrantHandler
    : ICommandHandler<ValidateTokenGrantCommand<CibaGrant>>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenGrantCommand<CibaGrant> command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenRequest, cibaGrant) = command;

        // see DefaultValidateTokenRequestHandler for additional validation

        // client_id/authenticated-client match is enforced upstream by DefaultClientAuthenticationService

        // an approved request always records the end-user that approved it
        if (cibaGrant.SubjectAuthentication is not { } subjectAuthentication)
            throw openIdContext
                .ErrorFactory.InvalidGrant(
                    "The backchannel authentication request has no approving end-user."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

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
