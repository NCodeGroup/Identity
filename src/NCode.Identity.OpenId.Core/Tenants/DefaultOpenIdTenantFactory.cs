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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Results;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the default implementation of the <see cref="IOpenIdTenantFactory"/> abstraction. Tenant selection is
/// delegated to the shared <see cref="ITenantResolver"/>; this type materializes the <see cref="OpenIdTenant"/>
/// (settings, secrets, issuer, base address) from the resolved <see cref="PersistedTenant"/> and caches it.
/// </summary>
[PublicAPI]
public class DefaultOpenIdTenantFactory(
    ITenantResolver tenantResolver,
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    ISecurityProfileCatalog securityProfileCatalog
) : IOpenIdTenantFactory
{
    /// <summary>
    /// Gets the <see cref="ITenantResolver"/> used to resolve the ambient tenant for the current request.
    /// </summary>
    protected ITenantResolver TenantResolver { get; } = tenantResolver;

    /// <summary>
    /// Gets the <see cref="TemplateBinderFactory"/> used to bind route templates.
    /// </summary>
    protected TemplateBinderFactory TemplateBinderFactory { get; } = templateBinderFactory;

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
    /// Gets the <see cref="ISecurityProfileCatalog"/> used to expand a scope's selected security profile.
    /// </summary>
    protected ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;

    /// <inheritdoc />
    public virtual async ValueTask<
        AsyncSharedReferenceLease<OpenIdTenant>
    > CreateTenantForRequestAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // Probe the cache before any store round-trip when the tenant's identity can be determined from the request
        // alone (static/path strategies). Host-based tenancy must map its domain to a tenant via the store, so it
        // falls through to a full resolve. A cache hit intentionally serves the cached tenant without re-reading the
        // store, so a tenant disabled after it was cached continues to serve until the cache entry expires.
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
        tenantId = persistedTenant.TenantId;

        var tenantPropertyBag = propertyBag.Clone();

        await using var tenantSettings = await GetTenantSettingsAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
            persistedTenant,
            tenantPropertyBag,
            cancellationToken
        );

        var tenantBaseAddress = await GetTenantBaseAddressAsync(
            httpContext,
            persistedTenant,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        var tenantIssuer = await GetTenantIssuerAsync(
            httpContext,
            persistedTenant,
            tenantBaseAddress,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        await using var tenantSecrets = await GetTenantSecretsAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
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
        OpenIdServer openIdServer,
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // Load the addressed tenant directly by identifier rather than resolving it from the request, since the request
        // is not addressed to this tenant. The shared tenant cache is intentionally not consulted or written: it holds
        // request-materialized tenants (whose base address derives from the request host), and an admin materialization
        // derives its base address from the tenant's own issuer instead, so the two must not alias each other.
        var persistedTenant = await LoadPersistedTenantAsync(tenantId, cancellationToken);

        var tenantPropertyBag = propertyBag.Clone();

        await using var tenantSettings = await GetTenantSettingsAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
            persistedTenant,
            tenantPropertyBag,
            cancellationToken
        );

        // The tenant's own issuer is its request-independent identity; it is both the issuer and the base address for
        // an admin materialization, so the tenant must have one.
        var tenantIssuer = GetConfiguredTenantIssuer(persistedTenant);
        var tenantBaseAddress = ParseBaseAddress(tenantIssuer);

        await using var tenantSecrets = await GetTenantSecretsAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
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
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantSecrets,
            tenantPropertyBag,
            cancellationToken
        );

        return tenantReference.AddReference();
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
            throw TypedResults
                .NotFound()
                .AsException($"A tenant with id '{tenantId}' could not be found.");

        if (persistedTenant.IsDisabled)
            throw TypedResults
                .NotFound()
                .AsException($"The tenant with id '{tenantId}' is disabled.");

        return persistedTenant;
    }

    private static string GetConfiguredTenantIssuer(PersistedTenant persistedTenant)
    {
        var tenantIssuer = persistedTenant.Issuer;
        if (!string.IsNullOrEmpty(tenantIssuer))
        {
            return tenantIssuer;
        }

        throw new InvalidOperationException(
            $"The tenant with id '{persistedTenant.TenantId}' has no issuer and cannot be materialized "
                + "outside of an inbound request addressed to it."
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
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="IReadOnlySettingCollectionProvider"/> instance.</returns>
    protected virtual ValueTask<
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider>
    > GetTenantSettingsAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        PersistedTenant persistedTenant,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var jsonOptions = openIdEnvironment.JsonSerializerOptions;
        var initialSettingsJson = persistedTenant.Settings.Value;
        var initialSettings = SecurityProfileCatalog.Expand(
            initialSettingsJson,
            OpenIdSettingScopes.Tenant,
            jsonOptions
        );

        var periodicPollingSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            new RefreshSettingsState(openIdEnvironment, openIdServer, persistedTenant),
            initialSettings,
            OpenIdOptions.Tenant.SettingsPeriodicRefreshInterval,
            RefreshSettingsAsync
        );

        var dataSources = new List<ICollectionDataSource<Setting>>
        {
            openIdServer.SettingsProvider.AsDataSource(),
            periodicPollingSource,
        };

        var provider = SettingCollectionProviderFactory.Create(dataSources, owns: true);

        return ValueTask.FromResult(provider.AsSharedReference());
    }

    private readonly record struct RefreshSettingsState(
        OpenIdEnvironment OpenIdEnvironment,
        OpenIdServer OpenIdServer,
        PersistedTenant PersistedTenant
    );

    private async ValueTask<RefreshCollectionResult<Setting>> RefreshSettingsAsync(
        RefreshSettingsState state,
        IReadOnlyCollection<Setting> current,
        CancellationToken cancellationToken
    )
    {
        var (openIdEnvironment, _, persistedTenant) = state;

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var tenantId = persistedTenant.TenantId;

        var newSettings = await store.GetSettingsAsync(tenantId, cancellationToken);

        var prevSettings = persistedTenant.Settings;
        var prevConcurrencyToken = prevSettings.ConcurrencyToken;
        var newConcurrencyToken = newSettings.ConcurrencyToken;

        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<Setting>();

        var jsonOptions = openIdEnvironment.JsonSerializerOptions;
        var settings = SecurityProfileCatalog.Expand(
            newSettings.Value,
            OpenIdSettingScopes.Tenant,
            jsonOptions
        );

        // update the state after successfully deserializing the settings
        persistedTenant.Settings = newSettings;

        return RefreshCollectionResultFactory.Changed(settings);
    }

    /// <summary>
    /// Used to get the tenant's <see cref="UriDescriptor"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="UriDescriptor"/> instance.</returns>
    protected virtual ValueTask<UriDescriptor> GetTenantBaseAddressAsync(
        HttpContext httpContext,
        PersistedTenant persistedTenant,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var httpRequest = httpContext.Request;

        // The tenant's base address is what every absolute URL the server publishes is built from — the issuer, the
        // discovery endpoint URLs, the registration_client_uri, the device verification_uri. A configured origin is
        // therefore preferred over anything the caller sent: deriving it from the request lets a Host header decide
        // where the server tells clients to send their credentials, which is only acceptable on a developer's machine.
        // The tenant's own issuer is its request-independent identity and names exactly where the tenant is served,
        // tenant route included, so it is the base address as-is — the explicit-tenant path above does the same (ADR-0138).
        if (!string.IsNullOrEmpty(persistedTenant.Issuer))
        {
            var issuerBaseAddress = ParseBaseAddress(persistedTenant.Issuer);
            return ValueTask.FromResult(issuerBaseAddress);
        }

        // The development fallback derives the base address from the request: its origin, the path base the host is
        // mounted under, and the tenant route bound from the request's route values.
        var basePath = httpRequest.PathBase;

        var tenantRoute = TenantResolver.GetTenantRoute();

        // Bind only the tenant route's own parameters, from a copy: the binder consumes the values it is given and
        // writes any it does not use as a query string, so handing it the request's live route values would strip
        // them from the request and leak the endpoint's other parameters into the base address.
        var tenantRouteValues = new RouteValueDictionary();
        foreach (var parameter in tenantRoute.Parameters)
        {
            if (httpRequest.RouteValues.TryGetValue(parameter.Name, out var value))
                tenantRouteValues[parameter.Name] = value;
        }

        var templateBinder = TemplateBinderFactory.Create(tenantRoute);
        var tenantRouteUrl = templateBinder.BindValues(tenantRouteValues);
        if (!string.IsNullOrEmpty(tenantRouteUrl))
        {
            basePath = basePath.Add(tenantRouteUrl);
        }

        var baseAddress = new UriDescriptor
        {
            Scheme = httpRequest.Scheme,
            Host = httpRequest.Host,
            Path = basePath,
        };

        return ValueTask.FromResult(baseAddress);
    }

    /// <summary>
    /// Used to get the tenant's <c>issuer identifier</c> from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <c>issuer identifier</c>.</returns>
    protected virtual ValueTask<string> GetTenantIssuerAsync(
        HttpContext httpContext,
        PersistedTenant persistedTenant,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        // The tenant's own issuer is its request-independent identity; the base address (which is derived from that
        // issuer when one is set) is the development-only fallback.
        var tenantIssuer = persistedTenant.Issuer;

        return ValueTask.FromResult(
            string.IsNullOrEmpty(tenantIssuer) ? tenantBaseAddress.ToString() : tenantIssuer
        );
    }

    /// <summary>
    /// Used to get the tenant's <see cref="ISecretKeyCollectionProvider"/> instance.
    /// The default implementation uses a periodic polling collection data source to periodically refresh the collection of secrets.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
    /// <param name="tenantIssuer">The <c>issuer identifier</c> for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    protected virtual ValueTask<
        AsyncSharedReferenceLease<ISecretKeyCollectionProvider>
    > GetTenantSecretsAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        PersistedTenant persistedTenant,
        string tenantIssuer,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var refreshInterval = OpenIdOptions.Tenant.SecretsPeriodicRefreshInterval;
        var initialCollection = SecretSerializer.DeserializeSecrets(persistedTenant.Secrets.Value);

        var dataSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            persistedTenant,
            initialCollection,
            refreshInterval,
            RefreshSecretsAsync
        );

        var provider = SecretKeyCollectionProviderFactory.Create(dataSource, owns: true);

        return ValueTask.FromResult(provider.AsSharedReference());
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

        var secrets = SecretSerializer.DeserializeSecrets(newSecrets.Value);

        // update the state after successfully deserializing the secrets
        persistedTenant.Secrets = newSecrets;

        return RefreshCollectionResultFactory.Changed(secrets);
    }

    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="persistedTenant">The <see cref="PersistedTenant"/> for the current tenant.</param>
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
            persistedTenant.DisplayName,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantSecrets,
            propertyBag
        );

        return ValueTask.FromResult(tenant.AsSharedReference());
    }
}
