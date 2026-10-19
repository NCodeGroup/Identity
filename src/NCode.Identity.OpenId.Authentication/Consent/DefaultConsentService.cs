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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue.Models;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Provides a default implementation of <see cref="IConsentService"/> that persists durable, granular consent through
/// the dedicated <see cref="IConsentStore"/> (keyed by tenant, client, and subject). A consent record tracks both the
/// scopes the subject was asked to decide on (the "decided" universe, grown monotonically) and the subset the subject
/// granted; the authorization endpoint shows the consent screen only when a request introduces a previously-undecided
/// scope, and issues the intersection of the request's scopes with the granted subset (OIDC down-scoping).
/// </summary>
internal class DefaultConsentService(
    TimeProvider timeProvider,
    IStoreManagerFactory storeManagerFactory,
    IPersistedGrantService persistedGrantService,
    IAmbientTenantAccessor ambientTenantAccessor,
    IOpenIdClientResolver clientResolver,
    IScopeDescriptionService scopeDescriptionService
) : IConsentService
{
    // Scope values are case-sensitive per RFC 6749.
    private static readonly StringComparer ScopeComparer = StringComparer.Ordinal;

    private TimeProvider TimeProvider { get; } = timeProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;
    private IScopeDescriptionService ScopeDescriptionService { get; } = scopeDescriptionService;

    /// <inheritdoc />
    public async ValueTask<ConsentResolution> ResolveConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        IReadOnlyCollection<string> requestedScopes,
        CancellationToken cancellationToken
    )
    {
        if (requestedScopes.Count == 0)
        {
            return new ConsentResolution(IsDecided: true, GrantedScopes: []);
        }

        var consent = await ReadConsentAsync(openIdContext, clientId, subjectId, cancellationToken);
        if (consent is null)
        {
            return new ConsentResolution(IsDecided: false, GrantedScopes: []);
        }

        var decided = new HashSet<string>(consent.RequestedScopes, ScopeComparer);
        var granted = new HashSet<string>(consent.GrantedScopes, ScopeComparer);

        var isDecided = requestedScopes.All(decided.Contains);
        var grantedSubset = requestedScopes.Where(granted.Contains).ToList();

        return new ConsentResolution(isDecided, grantedSubset);
    }

    /// <inheritdoc />
    public async ValueTask GrantConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        IReadOnlyCollection<string> requestedScopes,
        IReadOnlyCollection<string> grantedScopes,
        CancellationToken cancellationToken
    )
    {
        // Never trust a granted scope that was not part of what was requested/presented.
        var requestedSet = new HashSet<string>(requestedScopes, ScopeComparer);
        var grantedSet = new HashSet<string>(
            grantedScopes.Where(requestedSet.Contains),
            ScopeComparer
        );

        using var tenantScope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IConsentStore>();

        var utcNow = TimeProvider.GetUtcNowWithPrecisionInSeconds();
        var existing = await store.GetOrDefaultAsync(clientId, subjectId, cancellationToken);

        // The decided universe grows monotonically; the granted subset is replaced in place for the requested scopes
        // (so re-consent can revoke a previously-granted scope) while preserving decisions outside this request.
        var decided = new HashSet<string>(existing?.RequestedScopes ?? [], ScopeComparer);
        decided.UnionWith(requestedScopes);

        var granted = new HashSet<string>(existing?.GrantedScopes ?? [], ScopeComparer);
        granted.ExceptWith(requestedSet);
        granted.UnionWith(grantedSet);

        var consent = new PersistedConsent
        {
            TenantId = openIdContext.Tenant.TenantId,
            ClientId = clientId,
            SubjectId = subjectId,
            RequestedScopes = decided.ToList(),
            GrantedScopes = granted.ToList(),
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
        using var tenantScope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IConsentStore>();

        await store.RemoveAsync(clientId, subjectId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AuthorizedApplication>> GetAuthorizedApplicationsAsync(
        OpenIdContext openIdContext,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        List<PersistedConsent> consents;
        using (AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId))
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IConsentStore>();
            consents = (await store.GetBySubjectAsync(subjectId, cancellationToken))
                // Only surface applications the subject actually has standing grants for.
                .Where(consent => consent.GrantedScopes.Count > 0)
                .ToList();
        }

        var applications = new List<AuthorizedApplication>(consents.Count);
        foreach (var consent in consents)
        {
            var client = await ClientResolver.ResolveClientOrDefaultAsync(
                openIdContext,
                consent.ClientId,
                cancellationToken
            );

            var descriptions = await ScopeDescriptionService.DescribeScopesAsync(
                openIdContext.Tenant.TenantId,
                consent.GrantedScopes,
                cancellationToken
            );

            var scopes = consent
                .GrantedScopes.Select(value => new ConsentScope(
                    value,
                    descriptions.GetValueOrDefault(value),
                    IsGranted: true
                ))
                .ToList();

            applications.Add(
                new AuthorizedApplication(consent.ClientId, ResolveClientName(client), scopes)
            );
        }

        return applications
            .OrderBy(app => app.ClientName ?? app.ClientId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <inheritdoc />
    public async ValueTask<PendingConsent?> ResolvePendingConsentAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string returnUrl,
        CancellationToken cancellationToken
    )
    {
        var authorizationRequest = await ResolvePendingRequestAsync(
            openIdContext,
            returnUrl,
            cancellationToken
        );
        if (authorizationRequest is null)
        {
            return null;
        }

        var clientId = authorizationRequest.ClientId;
        var requestedScopes = authorizationRequest.Scopes;

        var resolution = await ResolveConsentAsync(
            openIdContext,
            clientId,
            subjectId,
            requestedScopes,
            cancellationToken
        );
        var granted = new HashSet<string>(resolution.GrantedScopes, ScopeComparer);

        var client = await ClientResolver.ResolveClientOrDefaultAsync(
            openIdContext,
            clientId,
            cancellationToken
        );

        var descriptions = await ScopeDescriptionService.DescribeScopesAsync(
            openIdContext.Tenant.TenantId,
            requestedScopes,
            cancellationToken
        );

        var scopes = requestedScopes
            .Select(value => new ConsentScope(
                value,
                descriptions.GetValueOrDefault(value),
                granted.Contains(value)
            ))
            .ToList();

        return new PendingConsent(clientId, ResolveClientName(client), scopes);
    }

    private async ValueTask<PersistedConsent?> ReadConsentAsync(
        OpenIdContext openIdContext,
        string clientId,
        string subjectId,
        CancellationToken cancellationToken
    )
    {
        using var tenantScope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IConsentStore>();
        return await store.GetOrDefaultAsync(clientId, subjectId, cancellationToken);
    }

    private async ValueTask<IAuthorizationRequest?> ResolvePendingRequestAsync(
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

        return envelope.PayloadJson.Deserialize<IAuthorizationRequest>(
            openIdContext.Environment.JsonSerializerOptions
        );
    }

    private static string? ResolveClientName(OpenIdClient? client) =>
        client is not null
        && client.Settings.TryGetValue(OpenIdSettingKeys.ClientName, out var clientName)
        && !string.IsNullOrEmpty(clientName)
            ? clientName
            : null;

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
