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

using NCode.Identity.Claims;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Tokens.Handlers;

/// <summary>
/// Provides a default implementation for a <see cref="GetIdTokenSubjectClaimsCommand"/> handler that generates the subject
/// claims for an ID token. Custom claims are added by additional handlers provided by the application.
/// </summary>
internal class DefaultGetIdTokenSubjectClaimsHandler(
    IClaimsService claimsService,
    ISubjectIdentifierService subjectIdentifierService
) : ICommandHandler<GetIdTokenSubjectClaimsCommand>, ISupportMediatorPriority
{
    private IClaimsService ClaimsService { get; } = claimsService;
    private ISubjectIdentifierService SubjectIdentifierService { get; } = subjectIdentifierService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        GetIdTokenSubjectClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenContext, targetClaims) = command;
        var (tokenRequest, _, _, _) = tokenContext;

        if (!tokenRequest.SubjectAuthentication.HasValue)
        {
            return ValueTask.CompletedTask;
        }

        var subjectAuthentication = tokenRequest.SubjectAuthentication.Value;
        var sourceClaims = subjectAuthentication.Subject;

        var claimTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            JoseClaimNames.Payload.Idp,
            JoseClaimNames.Payload.Acr,
        };

        // auth_time is included when it is required by the request (a max_age request or an essential auth_time claim
        // request, OpenID Connect Core 1.0 §2 and §5.5.1.1) or when the client opts to always include it.
        if (
            openIdClient.Settings.GetValue(OpenIdSettingKeys.AlwaysIncludeAuthTime)
            || tokenRequest.AuthTimeRequired
            || IsEssential(tokenRequest.RequestClaims?.IdToken, JoseClaimNames.Payload.AuthTime)
        )
        {
            claimTypes.Add(JoseClaimNames.Payload.AuthTime);
        }

        // add standard claims requested by the client via the scope parameter
        // https://openid.net/specs/openid-connect-core-1_0.html#ScopeClaims
        //
        // OpenID Connect Core 1.0 §5.4: the scope-driven profile/email/address/phone claims are returned from the
        // UserInfo endpoint whenever an access token is issued (the code flow), and only placed in the ID token when no
        // access token is issued (for example a bare 'id_token' response). An access token issued alongside this ID
        // token is signalled by a non-null AccessToken on the request, so gate the scope-driven claims on its absence;
        // the account-sourced UserInfo enricher returns them to the caller instead.
        var accessTokenIssued = !string.IsNullOrEmpty(tokenRequest.AccessToken);
        var scopes = tokenRequest.EffectiveScopes;

        if (!accessTokenIssued)
        {
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
        }

        // claims requested via the OpenID Connect 'claims' request parameter (ADR-0039); value/values are advisory and
        // essential is best-effort, so a requested claim is emitted only when the subject actually has it.
        var requestedIdTokenClaims = tokenRequest.RequestClaims?.IdToken;
        if (requestedIdTokenClaims is not null)
        {
            claimTypes.UnionWith(requestedIdTokenClaims.Keys);
        }

        // 'sub' is emitted explicitly (projected to the client's public/pairwise identifier), never copied from the
        // source principal, so a pairwise client never sees the stable principal identifier even when it requests 'sub'
        // via the 'claims' parameter.
        claimTypes.Remove(JoseClaimNames.Payload.Sub);
        SubjectIdentifierService.AddProjectedSubjectClaim(
            openIdContext,
            openIdClient,
            subjectAuthentication,
            targetClaims
        );

        ClaimsService.CopyClaims(sourceClaims, targetClaims, preventDuplicates: true, claimTypes);

        ClaimsService.CopyClaims(
            sourceClaims,
            targetClaims,
            preventDuplicates: false,
            JoseClaimNames.Payload.Amr
        );

        return ValueTask.CompletedTask;
    }

    private static bool IsEssential(
        IReadOnlyDictionary<string, IRequestClaim?>? requestedClaims,
        string claimType
    ) =>
        requestedClaims is not null
        && requestedClaims.TryGetValue(claimType, out var requestClaim)
        && requestClaim?.Essential == true;
}
