// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a <see cref="ValidateSubjectAuthenticationCommand"/> handler that validates the end user's standing, so
/// every validation of a subject refuses an end user whose standing is refused: a grant, a session or a backchannel
/// authentication approval issues no token to a blocked principal or a disabled account.
/// </summary>
// See ADR-1229.
internal class DefaultValidateStandingOnSubjectAuthenticationHandler
    : ICommandHandler<ValidateSubjectAuthenticationCommand>
{
    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateSubjectAuthenticationCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, _, _, subjectAuthentication, operationDisposition, _) = command;

        // short-circuit if we already have an error
        if (operationDisposition.HasError)
        {
            return;
        }

        await openIdContext.Mediator.SendAsync(
            new ValidateSubjectStandingCommand(
                openIdContext,
                subjectAuthentication,
                operationDisposition
            ),
            cancellationToken
        );
    }
}
