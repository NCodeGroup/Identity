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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.DeviceCode;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="DeviceCodeGrant"/>.
/// </summary>
internal class DefaultValidateDeviceCodeGrantHandler
    : ICommandHandler<ValidateTokenGrantCommand<DeviceCodeGrant>>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenGrantCommand<DeviceCodeGrant> command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenRequest, deviceCodeGrant) = command;

        // see DefaultValidateTokenRequestHandler for additional validation

        // client_id/authenticated-client match is enforced upstream by DefaultClientAuthenticationService

        // an approved request always records the end-user that approved it
        if (deviceCodeGrant.SubjectAuthentication is not { } subjectAuthentication)
            throw openIdContext
                .ErrorFactory.InvalidGrant(
                    "The device authorization request has no approving end-user."
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
