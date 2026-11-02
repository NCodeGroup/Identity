// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenRequestCommand"/> message.
/// </summary>
internal class DefaultValidateTokenRequestHandler(IClientScopeService clientScopeService)
    : ICommandHandler<ValidateTokenRequestCommand>,
        ISupportMediatorPriority
{
    private IClientScopeService ClientScopeService { get; } = clientScopeService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ValidateTokenRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenRequest) = command;

        var errorFactory = openIdContext.ErrorFactory;

        // client_id/authenticated-client match is enforced upstream by DefaultClientAuthenticationService

        // A requested scope must be permitted by the client's grants (ADR-1026). A null scope (the 'scope' parameter
        // is OPTIONAL — RFC 6749 §3.3) is left null here so grants that reuse an original grant's scopes (refresh
        // token, token exchange) can distinguish "not requested" from an explicitly empty set.
        var requestedScopes = tokenRequest.Scopes;
        if (requestedScopes is not null)
        {
            var allowedScopes = await ClientScopeService.GetAllowedScopesAsync(
                openIdContext.Tenant.TenantId,
                openIdClient.ClientId,
                cancellationToken
            );
            if (requestedScopes.Except(allowedScopes).Any())
                // invalid_scope
                throw errorFactory
                    .InvalidScope()
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException();
        }

        // additional validation occurs in:
        // - DefaultSelectTokenGrantHandlerHandler
        // - The specific token grant handler
    }
}
