#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Errors;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Password;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="PasswordGrant"/>.
/// </summary>
public class DefaultValidatePasswordGrantHandler : ICommandHandler<ValidateTokenGrantCommand<PasswordGrant>>, ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenGrantCommand<PasswordGrant> command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, tokenRequest, passwordGrant) = command;
        var subjectAuthentication = passwordGrant.SubjectAuthentication;

        // see DefaultValidateTokenRequestHandler for additional validation

        // DefaultClientAuthenticationService already performs this check for us
        Debug.Assert(
            string.IsNullOrEmpty(tokenRequest.ClientId) ||
            string.Equals(openIdClient.ClientId, tokenRequest.ClientId, StringComparison.Ordinal));

        // validate the subject
        await ValidateSubjectAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            subjectAuthentication,
            cancellationToken
        );
    }

    private static async ValueTask ValidateSubjectAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdRequest openIdRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken)
    {
        var mediator = openIdContext.Mediator;
        var operationDisposition = new OperationDisposition<IOpenIdError>();

        await mediator.SendAsync(
            new ValidateSubjectCommand(
                openIdContext,
                openIdClient,
                openIdRequest,
                subjectAuthentication,
                operationDisposition
            ),
            cancellationToken
        );

        if (operationDisposition.HasError)
        {
            throw operationDisposition.Error
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }
    }
}
