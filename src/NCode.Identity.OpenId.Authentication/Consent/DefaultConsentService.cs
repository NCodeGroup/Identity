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

using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using NCode.Identity.Jose.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue.Models;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Provides a default implementation of <see cref="IConsentService"/> that persists durable consent through the
/// dedicated <see cref="IConsentStore"/> (keyed by tenant, client, and subject) and resolves the pending authorization
/// request by peeking — without consuming — the parked continuation grant.
/// </summary>
internal class DefaultConsentService(
    TimeProvider timeProvider,
    IStoreManagerFactory storeManagerFactory,
    IPersistedGrantService persistedGrantService
) : IConsentService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    /// <inheritdoc />
    public async ValueTask<bool> HasConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancellationToken
    )
    {
        if (requestedScopes.Count == 0)
            return true;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IConsentStore>();

        var consent = await store.GetOrDefaultAsync(clientId, subjectId, cancellationToken);
        if (consent is null)
            return false;

        var granted = new HashSet<string>(consent.GrantedScopes, StringComparer.Ordinal);
        return requestedScopes.All(granted.Contains);
    }

    /// <inheritdoc />
    public async ValueTask GrantConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IConsentStore>();

        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var existing = await store.GetOrDefaultAsync(clientId, subjectId, cancellationToken);

        // Widen (union) rather than replace, so a follow-up request for a superset of scopes keeps the earlier grants.
        var mergedScopes = new HashSet<string>(scopes, StringComparer.Ordinal);
        if (existing is not null)
        {
            mergedScopes.UnionWith(existing.GrantedScopes);
        }

        var consent = new PersistedConsent
        {
            TenantId = openIdContext.Tenant.TenantId,
            ClientId = clientId,
            SubjectId = subjectId,
            GrantedScopes = mergedScopes.ToList(),
            CreatedWhen = existing?.CreatedWhen ?? utcNow,
            UpdatedWhen = utcNow,
            ConcurrencyToken = string.Empty,
        };

        await store.SaveAsync(consent, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RevokeConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IConsentStore>();

        await store.RemoveAsync(clientId, subjectId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<PendingConsent?> ResolvePendingConsentAsync(
        OpenIdContext openIdContext,
        string returnUrl,
        CancellationToken cancellationToken
    )
    {
        var state = ExtractContinuationState(returnUrl);
        if (string.IsNullOrEmpty(state))
            return null;

        var grantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Continue,
            state
        );

        var grant = await PersistedGrantService.GetOrDefaultAsync<ContinueEnvelope>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (grant is not { Status: PersistedGrantStatus.Active } active)
            return null;

        var envelope = active.Payload;
        if (
            !string.Equals(
                envelope.Code,
                OpenIdConstants.ContinueCodes.Authorization,
                StringComparison.Ordinal
            )
        )
            return null;

        var authorizationRequest = envelope.PayloadJson.Deserialize<IAuthorizationRequest>(
            openIdContext.Environment.JsonSerializerOptions
        );
        if (authorizationRequest is null)
            return null;

        return new PendingConsent(authorizationRequest.ClientId, authorizationRequest.Scopes);
    }

    // The parked authorization request is addressed by the continue endpoint's opaque 'state' key, carried inside the
    // local return URL the consent surface was redirected to; the framework query parser decodes it.
    private static string? ExtractContinuationState(string returnUrl)
    {
        var separatorIndex = returnUrl.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex < 0)
            return null;

        var query = QueryHelpers.ParseQuery(returnUrl[separatorIndex..]);
        return query.TryGetValue("state", out var values) ? values.ToString() : null;
    }
}
