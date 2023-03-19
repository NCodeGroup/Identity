#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NIdentity.OpenId.Contexts;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class AuthorizeHandler : ICommandResponseHandler<AuthorizeCommand, IOpenIdResult>
{
    private IdentityServerOptions Options { get; }
    private ISystemClock SystemClock { get; }

    public AuthorizeHandler(IOptions<IdentityServerOptions> optionsAccessor, ISystemClock systemClock)
    {
        Options = optionsAccessor.Value;
        SystemClock = systemClock;
    }

    public async ValueTask<IOpenIdResult> HandleAsync(AuthorizeCommand command, CancellationToken cancellationToken)
    {
        var promptType = command.AuthorizationRequest.PromptType;

        // TODO: check if supported
        if (promptType.HasFlag(PromptTypes.CreateAccount))
        {
            // TODO: redirect to create account page
            throw new NotImplementedException();
        }

        var reAuthenticate = promptType.HasFlag(PromptTypes.Login) ||
                             promptType.HasFlag(PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            // TODO: redirect to login page
            // reason: Client requested re-authentication.
            throw new NotImplementedException();
        }

        if (command.AuthenticateResult.Ticket is not { Principal.Identity.IsAuthenticated: true })
        {
            // TODO: redirect to login page
            // reason: User not authenticated.
            throw new NotImplementedException();
        }

        var endpointContext = command.EndpointContext;
        var authenticationTicket = command.AuthenticateResult.Ticket;
        var client = command.AuthorizationRequest.Client;

        if (!await ValidateUserIsActiveAsync(
                endpointContext,
                authenticationTicket,
                client,
                cancellationToken))
        {
            // TODO: redirect to login page
            // reason: User not active.
            throw new NotImplementedException();
        }

        // TODO: check tenant

        // TODO: check IdP

        // check MaxAge
        var identity = authenticationTicket.Principal.Identity as ClaimsIdentity ?? throw new InvalidOperationException();
        if (!ValidateMaxAge(identity, command.AuthorizationRequest.MaxAge))
        {
            // TODO: redirect to login page
            // reason: MaxAge exceeded.
            throw new NotImplementedException();
        }

        // TODO: check local idp restrictions

        // TODO: check external idp restrictions

        // TODO: check client's user SSO timeout

        throw new NotImplementedException();
    }

    private async ValueTask<bool> ValidateUserIsActiveAsync(
        OpenIdEndpointContext endpointContext,
        AuthenticationTicket authenticationTicket,
        Client client,
        CancellationToken cancellationToken)
    {
        if (client == null) throw new ArgumentNullException(nameof(client));
        var validateUserIsActiveContext = new ValidateUserIsActiveContext(
            Options,
            endpointContext,
            authenticationTicket,
            client);

        await Options.Events.ValidateUserIsActive(validateUserIsActiveContext, cancellationToken);

        return validateUserIsActiveContext.IsActive;
    }

    private bool ValidateMaxAge(ClaimsIdentity identity, TimeSpan? maxAge)
    {
        /*
         * max_age
         * OPTIONAL. Maximum Authentication Age. Specifies the allowable elapsed time in seconds since the last time the
         * End-User was actively authenticated by the OP. If the elapsed time is greater than this value, the OP MUST attempt
         * to actively re-authenticate the End-User. (The max_age request parameter corresponds to the OpenID 2.0 PAPE
         * [OpenID.PAPE] max_auth_age request parameter.) When max_age is used, the ID Token returned MUST include an auth_time
         * Claim Value.
         */

        if (!maxAge.HasValue)
        {
            return true;
        }

        // TODO: RFC says to check against auth_time but should we also check against AuthenticationTicket.Properties.IssuedUtc?

        var authTimeClaim = identity.FindFirst(OpenIdConstants.JwtClaimTypes.AuthenticationTime);
        if (authTimeClaim is null || !long.TryParse(authTimeClaim.Value, out var authTimeSeconds))
        {
            return false;
        }

        var authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeSeconds);
        return authTime + maxAge.Value >= SystemClock.UtcNow;
    }
}
