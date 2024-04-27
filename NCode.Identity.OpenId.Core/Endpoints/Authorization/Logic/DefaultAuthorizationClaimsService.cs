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

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Logic.Authorization;
using NCode.Jose;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Logic;

// TODO: use setting: claims_supported

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationClaimsService"/> abstraction.
/// </summary>
public class DefaultAuthorizationClaimsService : IAuthorizationClaimsService
{
    /// <inheritdoc />
    public async IAsyncEnumerable<Claim> GetAccessTokenClaimsAsync(
        AuthorizationRequestContext authorizationRequestContext,
        AuthenticationTicket authenticationTicket,
        DateTimeOffset timestamp,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var openIdTenant = authorizationRequestContext.OpenIdContext.Tenant;

        var subject = authenticationTicket.Principal.Identity as ClaimsIdentity ??
                      throw new InvalidOperationException("The AuthenticationTicket must contain a ClaimsIdentity.");

        var claimTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            JoseClaimNames.Payload.Sub,
            JoseClaimNames.Payload.AuthTime
        };

        var hasAuthTime = false;

        var claims = subject.Claims
            .Where(claim => claimTypes.Contains(claim.Type));

        foreach (var claim in claims)
        {
            cancellationToken.ThrowIfCancellationRequested();

            hasAuthTime = hasAuthTime || claim.Type == JoseClaimNames.Payload.AuthTime;

            yield return claim;
        }

        if (!hasAuthTime)
        {
            var authTime = authenticationTicket.Properties.IssuedUtc ?? timestamp;

            yield return new Claim(
                JoseClaimNames.Payload.AuthTime,
                authTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64,
                openIdTenant.Issuer,
                openIdTenant.Issuer);
        }

        await ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Claim> GetIdTokenClaimsAsync(
        AuthorizationRequestContext authorizationRequestContext,
        AuthenticationTicket authenticationTicket,
        DateTimeOffset timestamp,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var authorizationRequest = authorizationRequestContext.AuthorizationRequest;
        var openIdTenant = authorizationRequestContext.OpenIdContext.Tenant;

        // required: sub, auth_time, idp, amr
        // optional: acr
        // add identity resource user claims
        // add custom user claims
        // remove protocol claims

        var subject = authenticationTicket.Principal.Identity as ClaimsIdentity ??
                      throw new InvalidOperationException("The AuthenticationTicket must contain a ClaimsIdentity.");

        var claimTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            JoseClaimNames.Payload.Sub,
            JoseClaimNames.Payload.AuthTime
        };

        // add standard claims requested by the client via the scope parameter
        // https://openid.net/specs/openid-connect-core-1_0.html#ScopeClaims

        if (authorizationRequest.Scopes.Contains(OpenIdConstants.ScopeTypes.Profile))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Profile);
        }

        if (authorizationRequest.Scopes.Contains(OpenIdConstants.ScopeTypes.Email))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Email);
        }

        if (authorizationRequest.Scopes.Contains(OpenIdConstants.ScopeTypes.Address))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Address);
        }

        if (authorizationRequest.Scopes.Contains(OpenIdConstants.ScopeTypes.Profile))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Phone);
        }

        // TODO: add specific claims requested by the client via the claims parameter
        // https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter

        // TODO: should we also include profile claims based on a client configuration setting?

        // TODO: other claims...
        // client_id
        // acr
        // amr
        // azp (https://bitbucket.org/openid/connect/issues/973/)
        // sid
        // cnf

        var hasAuthTime = false;

        var claims = subject.Claims
            .Where(claim => claimTypes.Contains(claim.Type));

        foreach (var claim in claims)
        {
            cancellationToken.ThrowIfCancellationRequested();

            hasAuthTime = hasAuthTime || claim.Type == JoseClaimNames.Payload.AuthTime;

            yield return claim;
        }

        if (!hasAuthTime)
        {
            var authTime = authenticationTicket.Properties.IssuedUtc ?? timestamp;

            yield return new Claim(
                JoseClaimNames.Payload.AuthTime,
                authTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64,
                openIdTenant.Issuer,
                openIdTenant.Issuer);
        }

        await ValueTask.CompletedTask;
    }
}
