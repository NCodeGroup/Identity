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

using System.Globalization;
using System.Security.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Tokens.Commands;

namespace NCode.Identity.OpenId.Tokens.Handlers;

// TODO: use setting: claims_supported

/// <summary>
/// Provides a default implementation for a <see cref="GetIdTokenSubjectClaimsCommand"/> handler that generates the subject
/// claims for an id token.
/// </summary>
public class DefaultGetIdTokenSubjectClaimsHandler : ICommandHandler<GetIdTokenSubjectClaimsCommand>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(GetIdTokenSubjectClaimsCommand command, CancellationToken cancellationToken)
    {
        var (openIdContext, _, tokenContext, subjectClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        var subject = tokenRequest.Subject ?? throw new InvalidOperationException("Subject is required.");

        // required: sub, auth_time, idp, amr
        // optional: acr
        // add identity resource user claims
        // add custom user claims
        // remove protocol claims

        var claimTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            JoseClaimNames.Payload.Sub,
            JoseClaimNames.Payload.AuthTime
        };

        // add standard claims requested by the client via the scope parameter
        // https://openid.net/specs/openid-connect-core-1_0.html#ScopeClaims

        var scopes = tokenRequest.Scopes ?? [];

        if (scopes.Contains(OpenIdConstants.ScopeTypes.Profile))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Profile);
        }

        if (scopes.Contains(OpenIdConstants.ScopeTypes.Email))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Email);
        }

        if (scopes.Contains(OpenIdConstants.ScopeTypes.Address))
        {
            claimTypes.UnionWith(OpenIdConstants.ClaimsByScope.Address);
        }

        if (scopes.Contains(OpenIdConstants.ScopeTypes.Phone))
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

            subjectClaims.Add(claim);
        }

        if (!hasAuthTime)
        {
            var issuer = openIdContext.Tenant.Issuer;
            var authTime = tokenRequest.AuthenticationProperties?.IssuedUtc ?? tokenRequest.CreatedWhen;

            var claim = new Claim(
                JoseClaimNames.Payload.AuthTime,
                authTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64,
                issuer,
                issuer,
                subject);

            subjectClaims.Add(claim);
        }

        return ValueTask.CompletedTask;
    }
}
