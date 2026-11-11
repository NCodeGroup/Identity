// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the default implementation of the <see cref="IOpenIdTenantFactory"/> abstraction. Tenant selection is
/// delegated to the shared <see cref="ITenantResolver"/>; this type seeds the resolved <see cref="PersistedTenant"/>
/// through the <see cref="ITenantProvisioner"/> and materializes the <see cref="OpenIdTenant"/> (settings, secrets,
/// issuer, base address) from it, and caches it. A tenant is always published under its own issuer.
/// </summary>
[PublicAPI]
public class DefaultOpenIdTenantFactory(
    ITenantResolver tenantResolver,
    IOptions<OpenIdOptions> optionsAccessor,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretProtectorProvider secretProtectorProvider,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IOpenIdTeamProvider teamProvider
) : IOpenIdTenantFactory
{
    /// <summary>
    /// Gets the <see cref="ITenantResolver"/> used to resolve the ambient tenant for the current request.
    /// </summary>
    protected ITenantResolver TenantResolver { get; } = tenantResolver;

    /// <summary>
    /// Gets the <see cref="OpenIdOptions"/> used to configure OpenID.
    /// </summary>
    protected OpenIdOptions OpenIdOptions { get; } = optionsAccessor.Value;

    /// <summary>
    /// Gets the <see cref="IStoreManagerFactory"/> used to create <see cref="IStoreManager"/> instances.
    /// </summary>
    protected IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <summary>
    /// Gets the <see cref="IOpenIdTenantCache"/> used to cache tenant instances.
    /// </summary>
    protected IOpenIdTenantCache TenantCache { get; } = tenantCache;

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollectionProviderFactory"/> used to create <see cref="IReadOnlySettingCollectionProvider"/> instances.
    /// </summary>
    protected IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; } =
        settingCollectionProviderFactory;

    /// <summary>
    /// Gets the <see cref="ISettingSerializer"/> used to serialize/deserialize settings.
    /// </summary>
    protected ISettingSerializer SettingSerializer { get; } = settingSerializer;

    /// <summary>
    /// Gets the <see cref="ISecretSerializer"/> used to serialize/deserialize secrets.
    /// </summary>
    protected ISecretSerializer SecretSerializer { get; } = secretSerializer;

    /// <summary>
    /// Gets the <see cref="ISecretProtectorProvider"/> used to get the <see cref="ISecretProtector"/> that unprotects a tenant's secrets.
    /// </summary>
    protected ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;

    /// <summary>
    /// Gets the <see cref="ISecretKeyCollectionProviderFactory"/> used to create <see cref="ISecretKeyCollectionProvider"/> instances.
    /// </summary>
    protected ISecretKeyCollectionProviderFactory SecretKeyCollectionProviderFactory { get; } =
        secretKeyCollectionProviderFactory;

    /// <summary>
    /// Gets the <see cref="ICollectionDataSourceFactory"/> used to create <see cref="ICollectionDataSource{T}"/> instances.
    /// </summary>
    protected ICollectionDataSourceFactory CollectionDataSourceFactory { get; } =
        collectionDataSourceFactory;

    /// <summary>
    /// Gets the <see cref="IEffectiveSettingsResolver"/> used to read each scope's persisted settings document.
    /// </summary>
    protected IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;

    /// <summary>
    /// Gets the <see cref="IOpenIdTeamProvider"/> used to get the team a tenant belongs to.
    /// </summary>
    protected IOpenIdTeamProvider TeamProvider { get; } = teamProvider;

    /// <inheritdoc />
    public virtual async ValueTask<
        AsyncSharedReferenceLease<OpenIdTenant>
    > CreateTenantForRequestAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // Probe the cache before any store round-trip when the tenant's identity can be determined from the request
        // alone (static/path strategies). Host-based tenancy must map its domain to a tenant via the store, so it
        // falls through to a full resolve. A cache hit intentionally serves the cached tenant without re-reading the
        // store. A committed change announces the tenant (TenantChangedCommand), and every replica evicts it when the
        // announcement arrives (ADR-1205). A replica that may have missed an announcement evicts each cached tenant
        // whose stored row changed when its subscription is restored (ADR-1454), and a cached tenant lives at most
        // OpenIdTenantOptions.TenantCacheLifetime (ADR-1219).
        PersistedTenant? persistedTenant = null;
        if (!TenantResolver.TryGetTenantId(httpContext, out var tenantId))
        {
            persistedTenant = await TenantResolver.ResolveTenantAsync(
                httpContext,
                cancellationToken
            );
            tenantId = persistedTenant.TenantId;
        }

        await using var cachedTenantReference = await TenantCache.TryGetAsync(
            tenantId,
            propertyBag,
            cancellationToken
        );

        if (cachedTenantReference.IsActive)
        {
            return cachedTenantReference.AddReference();
        }

        persistedTenant ??= await TenantResolver.ResolveTenantAsync(httpContext, cancellationToken);

        return await MaterializeTenantAsync(
            httpContext,
            openIdEnvironment,
            openIdPlatform,
            persistedTenant,
            propertyBag,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public virtual async ValueTask<
        AsyncSharedReferenceLease<OpenIdTenant>
    > CreateControlPlaneTenantAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // The control-plane tenant is identified by configuration rather than from the request, and it is shared through
        // the same cache as when a request addresses it. The cache is probed before any store round-trip, so a cache hit
        // serves it as a request tenant is served (see CreateTenantForRequestAsync).
        var tenantId = TenantResolver.GetControlPlaneTenantId();

        await using var cachedTenantReference = await TenantCache.TryGetAsync(
            tenantId,
            propertyBag,
            cancellationToken
        );

        if (cachedTenantReference.IsActive)
        {
            return cachedTenantReference.AddReference();
        }

        var persistedTenant = await TenantResolver.ResolveControlPlaneTenantAsync(
            httpContext,
            cancellationToken
        );

        return await MaterializeTenantAsync(
            httpContext,
            openIdEnvironment,
            openIdPlatform,
            persistedTenant,
            propertyBag,
            cancellationToken
        );
    }

    private async ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> MaterializeTenantAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        PersistedTenant resolvedTenant,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var tenantId = resolvedTenant.TenantId;
        var tenantPropertyBag = propertyBag.Clone();

        // The team is checked before the tenant is seeded: a disabled team answers 404 without running the seed
        // pipeline, so a suspended customer's rows are not written and repeated requests cost no seed. A tenant never
        // moves between teams, so the team taken here stays the tenant's team once the seed returns.
        await using var team = await GetEnabledTeamAsync(
            openIdEnvironment,
            openIdPlatform,
            resolvedTenant,
            tenantPropertyBag,
            cancellationToken
        );

        // A tenant is seeded whenever it is materialized: the bootstrap of a tenant created on demand, and the
        // reconciliation that brings an existing tenant up to date with the seed contributors (ADR-1166). The provisioner
        // is scoped (its mediator is request-scoped), so it is resolved from the request's services. The seed returns the
        // tenant row it committed, so the tenant carries the row, settings and secrets concurrency tokens of its own seed,
        // and a replica that revalidates its cached tenants after a missed announcement keeps it (ADR-1454, ADR-1463).
        var provisioner = httpContext.RequestServices.GetRequiredService<ITenantProvisioner>();
        var persistedTenant = await provisioner.SeedAsync(tenantId, cancellationToken);

        await using var tenantSettings = await GetTenantSettingsAsync(
            httpContext,
            openIdEnvironment,
            team.Value,
            persistedTenant,
            tenantPropertyBag,
            cancellationToken
        );

        var tenantIssuer = await GetTenantIssuerAsync(
            httpContext,
            persistedTenant,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        var tenantBaseAddress = await GetTenantBaseAddressAsync(
            httpContext,
            persistedTenant,
            tenantIssuer,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        await using var tenantSecrets = await GetTenantSecretsAsync(
            httpContext,
            persistedTenant,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        await using var newTenantReference = await CreateOpenIdTenantAsync(
            httpContext,
            persistedTenant,
            team,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantSecrets,
            tenantPropertyBag,
            cancellationToken
        );

        await TenantCache.SetAsync(
            tenantId,
            newTenantReference,
            tenantPropertyBag,
            cancellationToken
        );

        return newTenantReference.AddReference();
    }

    /// <inheritdoc />
    public virtual async ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateTenantByIdAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // Load the addressed tenant directly by identifier rather than resolving it from the request, since the request
        // is not addressed to this tenant. The shared tenant cache is intentionally not consulted or written, so an
        // administrative operation acts on the tenant as it is stored now rather than as a request last cached it.
        var persistedTenant = await LoadPersistedTenantAsync(tenantId, cancellationToken);

        var tenantPropertyBag = propertyBag.Clone();

        await using var team = await GetEnabledTeamAsync(
            openIdEnvironment,
            openIdPlatform,
            persistedTenant,
            tenantPropertyBag,
            cancellationToken
        );

        await using var tenantSettings = await GetTenantSettingsAsync(
            httpContext,
            openIdEnvironment,
            team.Value,
            persistedTenant,
            tenantPropertyBag,
            cancellationToken
        );

        // Like every materialized tenant, it is published under its own issuer, which is also its base address.
        var tenantIssuer = await GetTenantIssuerAsync(
            httpContext,
            persistedTenant,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        var tenantBaseAddress = await GetTenantBaseAddressAsync(
            httpContext,
            persistedTenant,
            tenantIssuer,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        await using var tenantSecrets = await GetTenantSecretsAsync(
            httpContext,
            persistedTenant,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        await using var tenantReference = await CreateOpenIdTenantAsync(
            httpContext,
            persistedTenant,
            team,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantSecrets,
            tenantPropertyBag,
            cancellationToken
        );

        return tenantReference.AddReference();
    }

    // A disabled team disables every tenant beneath it, with the same outcome as a disabled tenant.
    private async ValueTask<AsyncSharedReferenceLease<OpenIdTeam>> GetEnabledTeamAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        PersistedTenant persistedTenant,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var team = await TeamProvider.GetTeamAsync(
            openIdEnvironment,
            openIdPlatform,
            persistedTenant.TeamId,
            propertyBag,
            cancellationToken
        );

        if (team.Value.IsDisabled)
        {
            await team.DisposeAsync();
            throw TypedResults
                .NotFound()
                .AsException($"The tenant with id '{persistedTenant.TenantId}' is disabled.");
        }

        return team;
    }

    private async ValueTask<PersistedTenant> LoadPersistedTenantAsync(
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.GetOrDefaultAsync(tenantId, cancellationToken);
        if (persistedTenant is null)
            throw TenantErrors.NotFound(tenantId);

        if (persistedTenant.IsDisabled)
            throw TypedResults
                .NotFound()
                .AsException($"The tenant with id '{tenantId}' is disabled.");

        return persistedTenant;
    }

    private static string GetRequiredTenantIssuer(PersistedTenant persistedTenant)
    {
        var tenantIssuer = persistedTenant.Issuer;
        if (!string.IsNullOrEmpty(tenantIssuer))
        {
            return tenantIssuer;
        }

        // Every write path gives a tenant a strategy serves an issuer (ADR-1165), so a tenant without one is malformed
        // and fails closed (ADR-1395): deriving an origin from the request would let a caller's Host header choose
        // where the server tells clients to send their credentials (ADR-1129).
        throw new InvalidOperationException(
            $"The tenant with id '{persistedTenant.TenantId}' has no issuer and cannot be served. Give it an issuer "
                + "through the management API."
        );
    }

    private static UriDescriptor ParseBaseAddress(string issuer)
    {
        var uri = new Uri(issuer, UriKind.Absolute);
        return new UriDescriptor
        {
            Scheme = uri.Scheme,
            // The authority omits the scheme's default port, so the published URLs keep the issuer's own form
            // (https://op.example/...) instead of gaining an explicit :443 the issuer does not carry.
            Host = HostString.FromUriComponent(uri.Authority),
            Path = PathString.FromUriComponent(uri),
        };
    }

    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdTeam">The <see cref="OpenIdTeam"/> the current tenant belongs to; its settings are the parent of the tenant's settings.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="IReadOnlySettingCollectionProvider"/> instance.</returns>
    protected virtual ValueTask<
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider>
    > GetTenantSettingsAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdTeam openIdTeam,
        PersistedTenant persistedTenant,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var initialSettings = EffectiveSettingsResolver.ReadScopeSettings(
            openIdTeam.SettingsProvider.Collection,
            OpenIdSettingScopes.Tenant,
            persistedTenant.Settings.Value
        );

        var periodicPollingSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            new RefreshSettingsState(openIdEnvironment, openIdTeam, persistedTenant),
            initialSettings,
            OpenIdOptions.Tenant.SettingsPeriodicRefreshInterval,
            RefreshSettingsAsync
        );

        var dataSources = new List<ICollectionDataSource<Setting>>
        {
            openIdTeam.SettingsProvider.AsDataSource(),
            periodicPollingSource,
        };

        var provider = SettingCollectionProviderFactory.Create(dataSources, owns: true);

        return ValueTask.FromResult(provider.AsSharedReference());
    }

    private readonly record struct RefreshSettingsState(
        OpenIdEnvironment OpenIdEnvironment,
        OpenIdTeam OpenIdTeam,
        PersistedTenant PersistedTenant
    );

    private async ValueTask<RefreshCollectionResult<Setting>> RefreshSettingsAsync(
        RefreshSettingsState state,
        IReadOnlyCollection<Setting> current,
        CancellationToken cancellationToken
    )
    {
        var persistedTenant = state.PersistedTenant;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantId = persistedTenant.TenantId;

        var newSettings = await store.GetSettingsAsync(tenantId, cancellationToken);

        var prevSettings = persistedTenant.Settings;
        var prevConcurrencyToken = prevSettings.ConcurrencyToken;
        var newConcurrencyToken = newSettings.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<Setting>();

        var settings = EffectiveSettingsResolver.ReadScopeSettings(
            state.OpenIdTeam.SettingsProvider.Collection,
            OpenIdSettingScopes.Tenant,
            newSettings.Value
        );

        // update the state after successfully deserializing the settings
        persistedTenant.Settings = newSettings;

        return RefreshCollectionResultFactory.Changed(settings);
    }

    /// <summary>
    /// Used to get the tenant's <c>issuer identifier</c>: its request-independent identity, asserted in every token it
    /// mints and the origin of every URL the server publishes for it. The default implementation returns the tenant's
    /// stored issuer and fails closed when it has none, because nothing derives an issuer from the request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <c>issuer identifier</c>.</returns>
    /// <exception cref="InvalidOperationException">The tenant has no issuer.</exception>
    // See ADR-1129 (the issuer is the single authoritative origin) and ADR-1165 (every served tenant has an issuer).
    protected virtual ValueTask<string> GetTenantIssuerAsync(
        HttpContext httpContext,
        PersistedTenant persistedTenant,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(GetRequiredTenantIssuer(persistedTenant));

    /// <summary>
    /// Used to get the tenant's base address, the <see cref="UriDescriptor"/> every absolute URL the server publishes for
    /// the tenant is built from — the discovery endpoint URLs, the <c>registration_client_uri</c>, the device
    /// <c>verification_uri</c>. The default implementation parses it from the tenant's issuer, which names exactly where
    /// the tenant is served, tenant route included.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="tenantIssuer">The <c>issuer identifier</c> for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="UriDescriptor"/> instance.</returns>
    // See ADR-1138.
    protected virtual ValueTask<UriDescriptor> GetTenantBaseAddressAsync(
        HttpContext httpContext,
        PersistedTenant persistedTenant,
        string tenantIssuer,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(ParseBaseAddress(tenantIssuer));

    /// <summary>
    /// Used to get the tenant's <see cref="ISecretKeyCollectionProvider"/> instance.
    /// The default implementation uses a periodic polling collection data source to periodically refresh the collection of secrets.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="tenantIssuer">The <c>issuer identifier</c> for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    /// <exception cref="InvalidOperationException">A key of the tenant cannot be unprotected.</exception>
    protected virtual async ValueTask<
        AsyncSharedReferenceLease<ISecretKeyCollectionProvider>
    > GetTenantSecretsAsync(
        HttpContext httpContext,
        PersistedTenant persistedTenant,
        string tenantIssuer,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var refreshInterval = OpenIdOptions.Tenant.SecretsPeriodicRefreshInterval;
        var initialCollection = await DeserializeTenantSecretsAsync(
            persistedTenant,
            cancellationToken
        );

        var dataSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            persistedTenant,
            initialCollection,
            refreshInterval,
            RefreshSecretsAsync
        );

        var provider = SecretKeyCollectionProviderFactory.Create(dataSource, owns: true);

        return provider.AsSharedReference();
    }

    // A tenant key that cannot be unprotected means the master key that protected it, or the root key that wraps that
    // master key, is missing or has changed. The server never discards or replaces such a key (ADR-1169), so the
    // tenant fails with a reason an operator can act on instead of the bare cryptographic error.
    private async ValueTask<IReadOnlyCollection<SecretKey>> DeserializeTenantSecretsAsync(
        PersistedTenant persistedTenant,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var protector = await SecretProtectorProvider.GetProtectorForSecretsAsync(
                SecretOwner.ForTenant(persistedTenant.TenantId),
                persistedTenant.Secrets.Value,
                cancellationToken
            );

            return SecretSerializer.DeserializeSecrets(protector, persistedTenant.Secrets.Value);
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException(
                $"The keys of the tenant with id '{persistedTenant.TenantId}' cannot be unprotected, so the tenant "
                    + "cannot be served. The master key that protected them, or the root key that wraps it, is missing "
                    + "or has changed: restore it. The server does not discard or replace a key it cannot read.",
                exception
            );
        }
    }

    private async ValueTask<RefreshCollectionResult<SecretKey>> RefreshSecretsAsync(
        PersistedTenant persistedTenant,
        IReadOnlyCollection<SecretKey> current,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantId = persistedTenant.TenantId;

        var newSecrets = await store.GetSecretsAsync(tenantId, cancellationToken);

        var prevSecrets = persistedTenant.Secrets;
        var prevConcurrencyToken = prevSecrets.ConcurrencyToken;
        var newConcurrencyToken = newSecrets.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<SecretKey>();

        var protector = await SecretProtectorProvider.GetProtectorForSecretsAsync(
            SecretOwner.ForTenant(tenantId),
            newSecrets.Value,
            cancellationToken
        );

        var secrets = SecretSerializer.DeserializeSecrets(protector, newSecrets.Value);

        // update the state after successfully deserializing the secrets
        persistedTenant.Secrets = newSecrets;

        return RefreshCollectionResultFactory.Changed(secrets);
    }

    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="team">The lease on the <see cref="OpenIdTeam"/> the current tenant belongs to.</param>
    /// <param name="tenantIssuer">The <c>issuer identifier</c> for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="tenantSecrets">The <see cref="ISecretKeyCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="OpenIdTenant"/> instance.</returns>
    protected virtual ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateOpenIdTenantAsync(
        HttpContext httpContext,
        PersistedTenant persistedTenant,
        AsyncSharedReferenceLease<OpenIdTeam> team,
        string tenantIssuer,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        AsyncSharedReferenceLease<ISecretKeyCollectionProvider> tenantSecrets,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        OpenIdTenant tenant = new DefaultOpenIdTenant(
            persistedTenant.TenantId,
            persistedTenant.TeamId,
            persistedTenant.ConcurrencyToken,
            persistedTenant.DisplayName,
            tenantIssuer,
            persistedTenant.MtlsEndpointAliasAuthority,
            tenantBaseAddress,
            tenantSettings,
            tenantSecrets,
            team.AddReference(),
            propertyBag
        );

        return ValueTask.FromResult(tenant.AsSharedReference());
    }
}
