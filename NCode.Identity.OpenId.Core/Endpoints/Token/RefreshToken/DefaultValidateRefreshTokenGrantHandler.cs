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
    IOpenIdErrorFactory errorFactory
) : ICommandHandler<ValidateTokenGrantCommand<RefreshTokenGrant>>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    private IOpenIdError InvalidGrantError => ErrorFactory
        .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
        .WithStatusCode(StatusCodes.Status400BadRequest);

    private IOpenIdError InvalidScopeError => ErrorFactory
        .InvalidScope()
        .WithStatusCode(StatusCodes.Status400BadRequest);

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenGrantCommand<RefreshTokenGrant> command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, tokenRequest, refreshTokenGrant) = command;
        var (clientId, originalScopes, _, subjectAuthentication) = refreshTokenGrant;
        var settings = openIdClient.Settings;

        if (!string.Equals(clientId, openIdClient.ClientId, StringComparison.Ordinal))
            throw InvalidGrantError.AsException("The refresh token belongs to a different client.");

        if (!settings.ScopesSupported.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
            throw InvalidGrantError.AsException("The client is prohibited from using refresh tokens.");

        // scope
        var requestedScopes = tokenRequest.Scopes;
        var effectiveScopes = requestedScopes ?? originalScopes;

        // extra scopes
        var hasExtraScopes = requestedScopes?.Except(originalScopes).Any() ?? false;
        if (hasExtraScopes)
            throw InvalidScopeError.AsException("The requested scope exceeds the scope granted by the resource owner.");

        // scopes_supported
        var hasInvalidScopes = effectiveScopes.Except(settings.ScopesSupported).Any();
        if (hasInvalidScopes)
            throw InvalidScopeError.AsException();

        if (subjectAuthentication.HasValue)
        {
            var subjectIsActive = await GetSubjectIsActiveAsync(
                openIdContext,
                openIdClient,
                tokenRequest,
                subjectAuthentication.Value,
                cancellationToken);

            if (!subjectIsActive)
                throw InvalidGrantError.AsException("The end-user is not active.");
        }
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
