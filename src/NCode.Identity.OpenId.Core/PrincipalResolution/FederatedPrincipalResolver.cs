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
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Provides the default implementation of <see cref="IPrincipalResolver"/>, which resolves an authenticated caller to
/// a federated principal: the subject claim is interpreted first as a server-owned <c>PrincipalId</c> and, when that
/// does not match, as an upstream <c>(issuer, subject)</c> external connection identity. The source and issuer claim
/// names are per-tenant settings on the shared request environment (ADR-0035/ADR-0036).
/// </summary>
internal class FederatedPrincipalResolver(
    ICryptoService cryptoService,
    IFederatedIdentityLinkingPolicy linkingPolicy
) : IPrincipalResolver
{
    private ICryptoService CryptoService { get; } = cryptoService;
    private IFederatedIdentityLinkingPolicy LinkingPolicy { get; } = linkingPolicy;

    /// <inheritdoc />
    public async ValueTask<string?> ResolvePrincipalIdOrDefaultAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdContext.Tenant.SettingsProvider.Collection;

        var subject = user.FindFirstValue(
            settings.GetValue(OpenIdSettingKeys.PrincipalSourceClaim)
        );
        if (string.IsNullOrEmpty(subject))
        {
            return null;
        }

        // The subject may already be a server-owned PrincipalId (the steady state, where the server issued the token).
        var principalStore = storeManager.GetStore<IFederatedPrincipalStore>();
        var existingPrincipal = await principalStore.GetOrDefaultAsync(subject, cancellationToken);
        if (existingPrincipal is not null)
        {
            return existingPrincipal.PrincipalId;
        }

        // Otherwise the subject is an upstream value paired with its issuer.
        var issuer = user.FindFirstValue(settings.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim));
        if (string.IsNullOrEmpty(issuer))
        {
            return null;
        }

        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();
        var identity = await identityStore.GetByIssuerSubjectAsync(
            issuer,
            subject,
            cancellationToken
        );
        return identity?.PrincipalId;
    }

    /// <inheritdoc />
    public async ValueTask<string> ResolvePrincipalIdAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal user,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    )
    {
        var existing = await ResolvePrincipalIdOrDefaultAsync(
            openIdContext,
            user,
            storeManager,
            cancellationToken
        );
        if (existing is not null)
        {
            return existing;
        }

        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var subject = user.FindFirstValue(
            settings.GetValue(OpenIdSettingKeys.PrincipalSourceClaim)
        );
        var issuer = user.FindFirstValue(settings.GetValue(OpenIdSettingKeys.PrincipalIssuerClaim));

        // Without a resolvable upstream (issuer, subject) pair we cannot form a durable connection identity for
        // idempotent lookup, so the subject value is used as-is: for a non-federated caller the subject is already the
        // principal reference, and provisioning on every sight would create duplicates (ADR-0035).
        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(issuer))
        {
            return subject ?? string.Empty;
        }

        var decision = await LinkingPolicy.ResolveLinkAsync(
            openIdContext,
            user,
            storeManager,
            cancellationToken
        );

        var principalStore = storeManager.GetStore<IFederatedPrincipalStore>();
        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();

        string principalId;
        if (decision.LinkedPrincipalId is { } linkedPrincipalId)
        {
            principalId = linkedPrincipalId;
        }
        else
        {
            principalId = CryptoService.GenerateResourceId();
            await principalStore.AddAsync(
                new PersistedFederatedPrincipal
                {
                    PrincipalId = principalId,
                    ConcurrencyToken = string.Empty,
                },
                cancellationToken
            );
        }

        await identityStore.AddAsync(
            new PersistedFederatedIdentity
            {
                FederatedIdentityId = CryptoService.GenerateResourceId(),
                PrincipalId = principalId,
                Issuer = issuer,
                Subject = subject,
                JoinKey = decision.JoinKey,
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );

        return principalId;
    }
}
