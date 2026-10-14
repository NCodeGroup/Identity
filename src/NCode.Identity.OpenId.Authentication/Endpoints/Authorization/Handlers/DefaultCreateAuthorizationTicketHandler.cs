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
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="CreateAuthorizationTicketCommand"/> message.
/// </summary>
internal class DefaultCreateAuthorizationTicketHandler(
    TimeProvider timeProvider,
    IAuthorizationCodeService authorizationCodeService,
    ITokenService tokenService,
    ISessionStateService sessionStateService
) : ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IAuthorizationCodeService AuthorizationCodeService { get; } = authorizationCodeService;
    private ITokenService TokenService { get; } = tokenService;
    private ISessionStateService SessionStateService { get; } = sessionStateService;

    /// <inheritdoc />
    public async ValueTask<IAuthorizationTicket> HandleAsync(
        CreateAuthorizationTicketCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, authorizationRequest, subjectAuthentication) = command;

        var responseTypes = authorizationRequest.ResponseTypes;

        var ticket = AuthorizationTicket.Create(openIdContext.Environment);

        ticket.CreatedWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        ticket.State = authorizationRequest.State;
        ticket.Issuer = openIdContext.Tenant.Issuer;

        // Session Management 1.0: emit session_state (and maintain the OP browser-state cookie) only when enabled.
        if (
            openIdContext.Tenant.SettingsProvider.Collection.GetValue(
                OpenIdSettingKeys.SessionManagementSupported
            )
        )
        {
            ticket.SessionState = SessionStateService.CreateSessionState(
                openIdContext,
                openIdClient.ClientId,
                authorizationRequest.RedirectUri
            );
        }

        if (responseTypes.Contains(OpenIdConstants.ResponseTypes.Code))
        {
            var securityToken = await AuthorizationCodeService.CreateAuthorizationCodeAsync(
                openIdContext,
                openIdClient,
                authorizationRequest,
                subjectAuthentication,
                cancellationToken
            );

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
            Resources = authorizationRequest.Resources,
            AuthorizationCode = ticket.AuthorizationCode,
            SubjectAuthentication = subjectAuthentication,
            RequestClaims = authorizationRequest.Claims,
            // A max_age request makes auth_time mandatory in the ID token (OpenID Connect Core 1.0 §3.1.2.1).
            AuthTimeRequired = authorizationRequest.MaxAge.HasValue,
        };

        if (responseTypes.Contains(OpenIdConstants.ResponseTypes.Token))
        {
            // A front-channel (hybrid) access token is bound to the pre-registered DPoP key when the authorization
            // request carried dpop_jkt (RFC 9449 §10); otherwise it is a plain Bearer token. Hybrid response types are
            // not advertised by default (secure-by-default; RFC 9700 / OAuth 2.1).
            var dpopJkt = authorizationRequest.DPoPJwkThumbprint;
            var isDPoPBound = !string.IsNullOrEmpty(dpopJkt);
            var frontChannelRequest = isDPoPBound
                ? securityTokenRequest with
                {
                    Confirmation = new TokenConfirmation { JwkThumbprint = dpopJkt },
                }
                : securityTokenRequest;

            var securityToken = await TokenService.CreateAccessTokenAsync(
                openIdContext,
                openIdClient,
                frontChannelRequest,
                cancellationToken
            );

            ticket.AccessToken = securityToken.TokenValue;
            ticket.ExpiresIn = securityToken.TokenLifetime.Duration;
            ticket.TokenType = isDPoPBound
                ? OpenIdConstants.TokenTypes.DPoP
                : OpenIdConstants.TokenTypes.Bearer;
        }

        // ReSharper disable once InvertIf
        if (responseTypes.Contains(OpenIdConstants.ResponseTypes.IdToken))
        {
            var newRequest = securityTokenRequest with { AccessToken = ticket.AccessToken };

            var securityToken = await TokenService.CreateIdTokenAsync(
                openIdContext,
                openIdClient,
                newRequest,
                cancellationToken
            );

            ticket.IdToken = securityToken.TokenValue;
        }

        return ticket;
    }
}
