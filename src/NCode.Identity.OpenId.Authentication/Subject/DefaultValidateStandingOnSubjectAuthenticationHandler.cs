#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides a <see cref="ValidateSubjectAuthenticationCommand"/> handler that validates the end user's standing, so
/// every validation of a subject refuses an end user whose standing is refused: a grant, a session or a backchannel
/// authentication approval issues no token to a blocked principal or a disabled account.
/// </summary>
// See ADR-0229.
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
