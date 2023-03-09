#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NIdentity.OpenId.Contexts;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Requests;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class GetAuthorizationResponseHandler : IRequestResponseHandler<GetAuthorizationResponseRequest, IHttpResult>
{
    private IdentityServerOptions Options { get; }
    private ISystemClock SystemClock { get; }

    public GetAuthorizationResponseHandler(IOptions<IdentityServerOptions> optionsAccessor, ISystemClock systemClock)
    {
        SystemClock = systemClock;
        Options = optionsAccessor.Value;
    }

    public async ValueTask<IHttpResult> HandleAsync(GetAuthorizationResponseRequest request, CancellationToken cancellationToken)
    {
        var promptResult = await ProcessPromptAsync(request, cancellationToken);

        if (promptResult.IsError)
        {
        }

        if (promptResult.IsError || (!promptResult.IsNoResult && request.AuthorizationRequest.PromptType.HasFlag(PromptTypes.None)))
        {
            // TODO: return error
        }

        if (promptResult.IsNoResult)
        {
            // TODO: check consent
        }

        if (promptResult.IsError || (!promptResult.IsNoResult && request.AuthorizationRequest.PromptType.HasFlag(PromptTypes.None)))
        {
            // TODO: return error
        }

        throw new NotImplementedException();
    }

    public async ValueTask<PromptResult> ProcessPromptAsync(GetAuthorizationResponseRequest request, CancellationToken cancellationToken)
    {
        var promptType = request.AuthorizationRequest.PromptType;

        if (promptType.HasFlag(PromptTypes.CreateAccount))
        {
            return PromptResult.CreateAccountRequired();
        }

        var reAuthenticate = promptType.HasFlag(PromptTypes.Login) ||
                             promptType.HasFlag(PromptTypes.SelectAccount);

        if (reAuthenticate)
        {
            return PromptResult.LoginRequired("Client requested re-authentication.");
        }

        if (request.AuthenticateResult.Ticket is not { Principal.Identity.IsAuthenticated: true })
        {
            return PromptResult.LoginRequired("User not authenticated.");
        }

        var httpContext = request.AuthorizationRequest.HttpContext;
        var ticket = request.AuthenticateResult.Ticket;
        var client = request.AuthorizationRequest.Client;

        if (!await ValidateUserIsActiveAsync(
                httpContext,
                ticket,
                client,
                cancellationToken))
        {
            return PromptResult.LoginRequired("User not active.");
        }

        // TODO: check tenant

        // TODO: check IdP

        // check MaxAge
        var identity = ticket.Principal.Identity as ClaimsIdentity ?? throw new InvalidOperationException();
        if (!ValidateMaxAge(identity, request.AuthorizationRequest.MaxAge))
        {
            return PromptResult.LoginRequired("MaxAge exceeded.");
        }

        // TODO: check local idp restrictions

        // TODO: check external idp restrictions

        // TODO: check client's user SSO timeout

        return PromptResult.NoResult();
    }

    private async ValueTask<bool> ValidateUserIsActiveAsync(
        HttpContext httpContext,
        AuthenticationTicket ticket,
        Client client,
        CancellationToken cancellationToken)
    {
        var validateUserIsActiveContext = new ValidateUserIsActiveContext(
            Options,
            httpContext,
            ticket,
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
