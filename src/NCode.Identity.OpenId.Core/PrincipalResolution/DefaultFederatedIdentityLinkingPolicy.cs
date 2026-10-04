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
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Provides the default implementation of <see cref="IFederatedIdentityLinkingPolicy"/>: a new identity whose verified
/// join key (an email address) matches an existing identity is attached to that identity's principal; otherwise a new
/// principal is provisioned. Two already-established principals are never merged automatically. The join/verified claim
/// names and linking knobs are per-tenant settings on the shared request environment.
/// </summary>
internal class DefaultFederatedIdentityLinkingPolicy : IFederatedIdentityLinkingPolicy
{
    /// <inheritdoc />
    public async ValueTask<FederatedIdentityLinkDecision> ResolveLinkAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdContext.Tenant.SettingsProvider.Collection;

        var joinKey = user.FindFirstValue(
            settings.GetValue(OpenIdSettingKeys.FederatedIdentityJoinClaim)
        );
        if (string.IsNullOrEmpty(joinKey))
        {
            // No join key is asserted; nothing is recorded and nothing can be linked.
            return new FederatedIdentityLinkDecision();
        }

        // An unverified join key is neither recorded nor matchable, so it cannot become a takeover vector.
        if (
            settings.GetValue(OpenIdSettingKeys.FederatedIdentityRequireVerified)
            && !IsVerified(
                user,
                settings.GetValue(OpenIdSettingKeys.FederatedIdentityVerifiedClaim)
            )
        )
        {
            return new FederatedIdentityLinkDecision();
        }

        // Explicit-only linking records the verified join key for later explicit linking but never auto-attaches.
        if (settings.GetValue(OpenIdSettingKeys.FederatedIdentityExplicitOnly))
        {
            return new FederatedIdentityLinkDecision { JoinKey = joinKey };
        }

        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();
        var matches = await identityStore.GetByJoinKeyAsync(joinKey, cancellationToken);

        return new FederatedIdentityLinkDecision
        {
            LinkedPrincipalId = matches.Count > 0 ? matches[0].PrincipalId : null,
            JoinKey = joinKey,
        };
    }

    private static bool IsVerified(ClaimsPrincipal user, string verifiedClaimName)
    {
        var value = user.FindFirstValue(verifiedClaimName);
        return bool.TryParse(value, out var verified) && verified;
    }
}
