﻿#region Copyright Preamble

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
/// Provides a default implementation for a <see cref="GetAccessTokenSubjectClaimsCommand"/> handler that generates the
/// subject claims for an access token.
/// </summary>
public class DefaultGetAccessTokenSubjectClaimsHandler : ICommandHandler<GetAccessTokenSubjectClaimsCommand>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(GetAccessTokenSubjectClaimsCommand command, CancellationToken cancellationToken)
    {
        var (openIdContext, _, tokenContext, subjectClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        // TODO: what about client authentication?
        var subject = tokenRequest.SubjectAuthentication?.Subject ??
                      throw new InvalidOperationException("Subject is required.");

        var claimTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            JoseClaimNames.Payload.Sub,
            JoseClaimNames.Payload.AuthTime
        };

        var hasAuthTime = false;

        var claims = subject.Claims.Where(claim => claimTypes.Contains(claim.Type));

        foreach (var claim in claims)
        {
            cancellationToken.ThrowIfCancellationRequested();

            hasAuthTime = hasAuthTime || claim.Type == JoseClaimNames.Payload.AuthTime;

            subjectClaims.Add(claim);
        }

        if (!hasAuthTime)
        {
            var issuer = openIdContext.Tenant.Issuer;
            var authTime = tokenRequest.SubjectAuthentication?.AuthenticationProperties.IssuedUtc ?? tokenRequest.CreatedWhen;

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
