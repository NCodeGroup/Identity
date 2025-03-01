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
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;

namespace NCode.Identity.OpenId.Endpoints.Token.ClientCredentials;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="ClientCredentialsGrant"/>.
/// </summary>
public class DefaultValidateClientCredentialsGrantHandler(
    IOpenIdErrorFactory errorFactory
) : ICommandHandler<ValidateTokenGrantCommand<ClientCredentialsGrant>>, ISupportMediatorPriority
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    private IOpenIdError InvalidScopeError => ErrorFactory
        .InvalidScope()
        .WithStatusCode(StatusCodes.Status400BadRequest);

    /// <inheritdoc />
    public int MediatorPriority => DefaultOpenIdRegistration.MediatorPriority;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenGrantCommand<ClientCredentialsGrant> command,
        CancellationToken cancellationToken)
    {
        var (_, _, tokenRequest, _) = command;

        // see DefaultValidateTokenRequestHandler for additional validation

        // scopes
        var scopes = tokenRequest.Scopes ?? [];

        // scope: openid
        if (scopes.Contains(OpenIdConstants.ScopeTypes.OpenId))
            // invalid_scope
            throw InvalidScopeError
                .WithDescription("The 'openid' scope is not allowed with the 'client_credentials' grant type.")
                .AsException();

        // scope: offline_access (i.e. refresh_token)
        // https://www.rfc-editor.org/rfc/rfc6749#section-4.4.3
        if (scopes.Contains(OpenIdConstants.ScopeTypes.OfflineAccess))
            // invalid_scope
            throw InvalidScopeError
                .WithDescription("The 'offline_access' scope is not allowed with the 'client_credentials' grant type.")
                .AsException();

        return ValueTask.CompletedTask;
    }
}
