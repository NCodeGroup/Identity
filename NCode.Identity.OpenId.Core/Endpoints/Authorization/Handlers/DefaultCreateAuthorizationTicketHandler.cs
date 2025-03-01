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

using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Logic.Authorization;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Tokens;
using NCode.Identity.OpenId.Tokens.Models;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="CreateAuthorizationTicketCommand"/> message.
/// </summary>
public class DefaultCreateAuthorizationTicketHandler(
    TimeProvider timeProvider,
    IAuthorizationCodeService authorizationCodeService,
    ITokenService tokenService
) : ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IAuthorizationCodeService AuthorizationCodeService { get; } = authorizationCodeService;
    private ITokenService TokenService { get; } = tokenService;

    /// <inheritdoc />
    public async ValueTask<IAuthorizationTicket> HandleAsync(
        CreateAuthorizationTicketCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, authorizationRequest, subjectAuthentication) = command;

        var responseTypes = authorizationRequest.ResponseTypes;

        var ticket = AuthorizationTicket.Create(openIdContext.Environment);

        ticket.CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        ticket.State = authorizationRequest.State;
        ticket.Issuer = openIdContext.Tenant.Issuer;

        if (responseTypes.Contains(OpenIdConstants.ResponseTypes.Code))
        {
            var securityToken = await AuthorizationCodeService.CreateAuthorizationCodeAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                subjectAuthentication,
                cancellationToken);

            ticket.AuthorizationCode = securityToken.TokenValue;
        }

        var securityTokenRequest = new CreateSecurityTokenRequest
        {
            CreatedWhen = ticket.CreatedWhen,
            GrantType = authorizationRequest.GrantType,
            Nonce = authorizationRequest.Nonce,
            State = authorizationRequest.State,
            OriginalScopes = authorizationRequest.Scopes,
            EffectiveScopes = authorizationRequest.Scopes,
            AuthorizationCode = ticket.AuthorizationCode,
            SubjectAuthentication = subjectAuthentication
        };

        if (responseTypes.Contains(OpenIdConstants.ResponseTypes.Token))
        {
            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                securityTokenRequest,
                cancellationToken);

            ticket.AccessToken = securityToken.TokenValue;
            ticket.ExpiresIn = securityToken.TokenPeriod.Duration;
            ticket.TokenType = OpenIdConstants.TokenTypes.Bearer; // TODO: add support for DPoP
        }

        // ReSharper disable once InvertIf
        if (responseTypes.Contains(OpenIdConstants.ResponseTypes.IdToken))
        {
            var newRequest = securityTokenRequest with
            {
                AccessToken = ticket.AccessToken
            };

            var securityToken = await TokenService.CreateIdTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken);

            ticket.IdToken = securityToken.TokenValue;
        }

        return ticket;
    }
}
