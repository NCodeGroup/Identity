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
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Token.Password;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="PasswordGrant"/>.
/// </summary>
public class DefaultValidatePasswordGrantHandler(
    IOpenIdErrorFactory errorFactory
) : ICommandHandler<ValidateTokenGrantCommand<PasswordGrant>>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    private IOpenIdError InvalidGrantError => ErrorFactory
        .InvalidGrant("The provided password grant is invalid, expired, or revoked.")
        .WithStatusCode(StatusCodes.Status400BadRequest);

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

        var subjectIsActive = await GetSubjectIsActiveAsync(
            openIdContext,
            openIdClient,
            tokenRequest,
            subjectAuthentication,
            cancellationToken);

        if (!subjectIsActive)
            throw InvalidGrantError.AsException("The end-user is not active.");
    }

    private static async ValueTask<bool> GetSubjectIsActiveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdRequest openIdRequest,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken)
    {
        var result = new ValidateSubjectIsActiveResult();

        var command = new ValidateSubjectIsActiveCommand(
            openIdContext,
            openIdClient,
            openIdRequest,
            subjectAuthentication,
            result);

        var mediator = openIdContext.Mediator;
        await mediator.SendAsync(command, cancellationToken);

        return result.IsActive;
    }
}
