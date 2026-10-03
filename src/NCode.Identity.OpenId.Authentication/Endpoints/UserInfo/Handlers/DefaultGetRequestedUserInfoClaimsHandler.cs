#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Handlers;

/// <summary>
/// Provides a handler for <see cref="GetUserInfoClaimsCommand"/> that honors the UserInfo claims requested via the
/// OpenID Connect <c>claims</c> request parameter (ADR-0039). Those claims were persisted at access-token issuance as a
/// grant keyed by the token's <c>jti</c>; this handler resolves that grant and copies each requested claim from the
/// authenticated subject when present, additive to the scope-driven claims. <c>value</c>/<c>values</c> are advisory and
/// <c>essential</c> is best-effort, so a requested claim the subject does not carry is simply omitted.
/// </summary>
internal class DefaultGetRequestedUserInfoClaimsHandler(
    IPersistedGrantService persistedGrantService
) : ICommandHandler<GetUserInfoClaimsCommand>, ISupportMediatorPriority
{
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        GetUserInfoClaimsCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, subjectAuthentication, claims) = command;

        var subject = subjectAuthentication.Subject;
        var jti = subject.FindFirstValue(JoseClaimNames.Payload.Jti);
        if (string.IsNullOrEmpty(jti))
        {
            return;
        }

        var tenantId = openIdContext.Tenant.TenantId;
        var grantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.UserInfoClaims,
            jti
        );

        var grant = await PersistedGrantService.GetOrDefaultAsync<IRequestClaims>(
            openIdContext,
            grantId,
            cancellationToken
        );

        var requested = grant?.Payload.UserInfo;
        if (requested is null)
        {
            return;
        }

        foreach (var claimType in requested.Keys)
        {
            // do not override a claim another enricher already supplied (for example, from a user store)
            if (claims.ContainsKey(claimType))
            {
                continue;
            }

            var values = subject.FindAll(claimType).Select(claim => claim.Value).ToList();
            if (values.Count == 1)
            {
                claims[claimType] = values[0];
            }
            else if (values.Count > 1)
            {
                claims[claimType] = values;
            }
        }
    }
}
