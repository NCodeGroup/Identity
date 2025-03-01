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

using NCode.Identity.Jose;
using NCode.Identity.OpenId.Claims;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Tokens.Commands;

namespace NCode.Identity.OpenId.Tokens.Handlers;

/// <summary>
/// Provides a default implementation for a <see cref="GetIdTokenSubjectClaimsCommand"/> handler that generates the subject
/// claims for an id token. Custom claims are added by additional handlers provided by the application.
/// </summary>
public class DefaultGetIdTokenSubjectClaimsHandler(
    IClaimsService claimsService
) : ICommandHandler<GetIdTokenSubjectClaimsCommand>, ISupportMediatorPriority
{
    private IClaimsService ClaimsService { get; } = claimsService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultOpenIdRegistration.MediatorPriority;

    /// <inheritdoc />
    public ValueTask HandleAsync(GetIdTokenSubjectClaimsCommand command, CancellationToken cancellationToken)
    {
        var (_, _, tokenContext, targetClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        if (!tokenRequest.SubjectAuthentication.HasValue)
        {
            return ValueTask.CompletedTask;
        }

        var (_, _, sourceClaims, _) = tokenRequest.SubjectAuthentication.Value;

        var claimTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            JoseClaimNames.Payload.Idp,
            JoseClaimNames.Payload.Sub,
            JoseClaimNames.Payload.AuthTime,
        };

        // add standard claims requested by the client via the scope parameter
        // https://openid.net/specs/openid-connect-core-1_0.html#ScopeClaims

        var scopes = tokenRequest.EffectiveScopes;

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

        ClaimsService.CopyClaims(
            sourceClaims,
            targetClaims,
            preventDuplicates: true,
            claimTypes
        );

        ClaimsService.CopyClaims(
            sourceClaims,
            targetClaims,
            preventDuplicates: false,
            JoseClaimNames.Payload.Amr
        );

        return ValueTask.CompletedTask;
    }
}
