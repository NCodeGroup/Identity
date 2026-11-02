// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="RefreshTokenGrant"/>.
/// </summary>
internal class DefaultValidateRefreshTokenGrantHandler(IClientScopeService clientScopeService)
    : ICommandHandler<ValidateTokenGrantCommand<RefreshTokenGrant>>,
        ISupportMediatorPriority
{
    private IClientScopeService ClientScopeService { get; } = clientScopeService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenGrantCommand<RefreshTokenGrant> command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenRequest, refreshTokenGrant) = command;
        var (clientId, originalScopes, _, subjectAuthentication, _, _) = refreshTokenGrant;

        var errorFactory = openIdContext.ErrorFactory;

        // see DefaultValidateTokenRequestHandler for additional validation

        if (!string.Equals(clientId, openIdClient.ClientId, StringComparison.Ordinal))
            throw errorFactory
                .InvalidGrant("The provided refresh token is invalid, expired, or revoked.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException("The refresh token belongs to a different client.");

        // scope
        var requestedScopes = tokenRequest.Scopes;
        var effectiveScopes = requestedScopes ?? originalScopes;

        // extra scopes
        var hasExtraScopes = requestedScopes?.Except(originalScopes).Any() ?? false;
        if (hasExtraScopes)
            throw errorFactory
                .InvalidScope()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException(
                    "The requested scope exceeds the scope granted by the resource owner."
                );

        // A requested scope must be permitted by the client's grants (ADR-1026).
        var allowedScopes = await ClientScopeService.GetAllowedScopesAsync(
            openIdContext.Tenant.TenantId,
            openIdClient.ClientId,
            cancellationToken
        );
        if (effectiveScopes.Except(allowedScopes).Any())
            throw errorFactory
                .InvalidScope()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        // validate the subject
        if (subjectAuthentication.HasValue)
        {
            await TokenGrantSubjectValidation.ValidateAsync(
                openIdContext,
                openIdClient,
                tokenRequest,
                subjectAuthentication.Value,
                cancellationToken
            );
        }
    }
}
