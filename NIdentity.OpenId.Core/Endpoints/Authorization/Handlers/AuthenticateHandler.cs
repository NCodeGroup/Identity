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

using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using NCode.Jose;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Models;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class AuthenticateHandler : ICommandResponseHandler<AuthenticateCommand, AuthenticateResult>
{
    private IdentityServerOptions Options { get; }
    private ISystemClock SystemClock { get; }

    public AuthenticateHandler(IOptions<IdentityServerOptions> optionsAccessor, ISystemClock systemClock)
    {
        Options = optionsAccessor.Value;
        SystemClock = systemClock;
    }

    public async ValueTask<AuthenticateResult> HandleAsync(AuthenticateCommand command, CancellationToken cancellationToken)
    {
        var authenticateResult = await command.EndpointContext.HttpContext.AuthenticateAsync(Options.SignInScheme);

        // TODO: remove?
        if (authenticateResult.Succeeded)
            NormalizeAuthenticationClaims(authenticateResult.Principal, authenticateResult.Properties);

        return authenticateResult;
    }

    private void NormalizeAuthenticationClaims(ClaimsPrincipal principal, AuthenticationProperties properties)
    {
        var utcNow = SystemClock.UtcNow;
        var issuedWhen = properties.IssuedUtc;
        var expiresWhen = properties.ExpiresUtc;
        var identity = principal.Identities.First();

        string? iss = null;
        string? sub = null;
        DateTimeOffset? nbf = null;
        DateTimeOffset? exp = null;
        DateTimeOffset? authTime = null;

        foreach (var claim in principal.Claims)
        {
            if (iss is null && EqualsIgnoreCase(claim.Type, JoseClaimNames.Payload.Iss))
            {
                iss = claim.Value;
            }

            if (sub is null && EqualsIgnoreCase(claim.Type, JoseClaimNames.Payload.Sub))
            {
                sub = claim.Value;
            }

            if (nbf is null &&
                EqualsIgnoreCase(claim.Type, JoseClaimNames.Payload.Nbf) &&
                long.TryParse(claim.Value, out var nbfUnixSeconds))
            {
                nbf = DateTimeOffset.FromUnixTimeSeconds(nbfUnixSeconds);
            }

            if (exp is null &&
                EqualsIgnoreCase(claim.Type, JoseClaimNames.Payload.Exp) &&
                long.TryParse(claim.Value, out var expUnixSeconds))
            {
                exp = DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds);
            }

            if (authTime is null &&
                EqualsIgnoreCase(claim.Type, JoseClaimNames.Payload.AuthTime) &&
                long.TryParse(claim.Value, out var authTimeUnixSeconds))
            {
                authTime = DateTimeOffset.FromUnixTimeSeconds(authTimeUnixSeconds);
            }
        }

        if (iss is null)
        {
            // TODO: use better exception
            throw new InvalidOperationException("The 'iss' claim is missing.");
        }

        if (sub is null)
        {
            // TODO: use better exception
            throw new InvalidOperationException("The 'sub' claim is missing.");
        }

        if (issuedWhen is null)
        {
            properties.IssuedUtc = issuedWhen = nbf ?? utcNow;
        }

        if (expiresWhen is null)
        {
            // TODO: use better exception
            properties.ExpiresUtc = expiresWhen = exp ?? throw new InvalidOperationException("Unable to determine when the authentication ticket expires.");
        }

        if (authTime is null)
        {
            authTime = issuedWhen;

            identity.AddClaim(new Claim(
                JoseClaimNames.Payload.AuthTime,
                authTime.Value.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64,
                iss,
                iss));
        }

        var normalizedAuthenticationClaims = new NormalizedAuthenticationClaims
        {
            Issuer = iss,
            Subject = sub,
            IssuedWhen = issuedWhen.Value,
            ExpiresWhen = expiresWhen.Value,
            AuthTime = authTime.Value
        };

        properties.Parameters[NormalizedAuthenticationClaims.Key] = normalizedAuthenticationClaims;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualsIgnoreCase(string x, string y) =>
        string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
}
