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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Subject;

namespace NCode.Identity.OpenId.Endpoints.Token.RefreshToken;

/// <summary>
/// Provides a default implementation of handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="RefreshTokenGrant"/>.
/// </summary>
public class DefaultValidateRefreshTokenGrantHandler(
    IOpenIdErrorFactory errorFactory,
    ISubjectService subjectService
) : ICommandHandler<ValidateTokenGrantCommand<RefreshTokenGrant>>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;
    private ISubjectService SubjectService { get; } = subjectService;

    private IOpenIdError InvalidGrantError => ErrorFactory
        .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
        .WithStatusCode(StatusCodes.Status400BadRequest);

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenGrantCommand<RefreshTokenGrant> command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, tokenRequest, refreshTokenGrant) = command;
        var (clientId, scopes, subjectAuthentication) = refreshTokenGrant;

        if (!string.Equals(clientId, openIdClient.ClientId, StringComparison.Ordinal))
            throw InvalidGrantError.AsException("The refresh token belongs to a different client.");

        // TODO: AllowOfflineAccess/DenyOfflineAccessScope

        // TODO: ValidateScopes

        if (subjectAuthentication.HasValue)
        {
            var isSubjectActive = await ValidateSubjectIsActiveAsync(
                openIdContext,
                openIdClient,
                tokenRequest,
                subjectAuthentication.Value,
                cancellationToken);

            if (!isSubjectActive)
                throw InvalidGrantError.AsException("The subject is not active.");
        }
    }

    private static async ValueTask<bool> ValidateSubjectIsActiveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IOpenIdMessage openIdMessage,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken)
    {
        var result = new ValidateSubjectIsActiveResult();

        var command = new ValidateSubjectIsActiveCommand(
            openIdContext,
            openIdClient,
            openIdMessage,
            subjectAuthentication,
            result);

        var mediator = openIdContext.Mediator;
        await mediator.SendAsync(command, cancellationToken);

        return result.IsActive;
    }
}
