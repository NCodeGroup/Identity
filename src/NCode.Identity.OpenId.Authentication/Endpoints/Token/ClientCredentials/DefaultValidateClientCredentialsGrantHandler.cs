// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.ClientCredentials;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="ClientCredentialsGrant"/>.
/// </summary>
internal class DefaultValidateClientCredentialsGrantHandler
    : ICommandHandler<ValidateTokenGrantCommand<ClientCredentialsGrant>>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenGrantCommand<ClientCredentialsGrant> command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, _, tokenRequest, _) = command;

        var errorFactory = openIdContext.ErrorFactory;

        // see DefaultValidateTokenRequestHandler for additional validation

        // scopes
        var scopes = tokenRequest.Scopes ?? [];

        // scope: openid
        if (scopes.Contains(OpenIdProtocol.ScopeTypes.OpenId))
            // invalid_scope
            throw errorFactory
                .InvalidScope()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithDescription(
                    "The 'openid' scope is not allowed with the 'client_credentials' grant type."
                )
                .AsException();

        // scope: offline_access (i.e. refresh_token)
        // https://www.rfc-editor.org/rfc/rfc6749#section-4.4.3
        if (scopes.Contains(OpenIdProtocol.ScopeTypes.OfflineAccess))
            // invalid_scope
            throw errorFactory
                .InvalidScope()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithDescription(
                    "The 'offline_access' scope is not allowed with the 'client_credentials' grant type."
                )
                .AsException();

        return ValueTask.CompletedTask;
    }
}
