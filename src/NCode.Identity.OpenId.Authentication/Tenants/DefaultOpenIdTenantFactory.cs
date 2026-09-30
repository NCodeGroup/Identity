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
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.Models;
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Authentication.Servers;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Authentication.Tenants;

/// <summary>
/// Provides the default implementation of the <see cref="IOpenIdTenantFactory"/> abstraction. Tenant selection is
/// delegated to the shared <see cref="ITenantSelector"/>; this type materializes the <see cref="OpenIdTenant"/>
/// (settings, secrets, issuer, base address) from the resolved <see cref="PersistedTenant"/> and caches it.
/// </summary>
[PublicAPI]
public class DefaultOpenIdTenantFactory(
    ITenantSelector tenantSelector,
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory
) : IOpenIdTenantFactory
{
    /// <summary>
    /// Gets the <see cref="ITenantSelector"/> used to resolve the ambient tenant for the current request.
    /// </summary>
    protected ITenantSelector TenantSelector { get; } = tenantSelector;

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

    /// <inheritdoc />
    public virtual async ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateTenantAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var persistedTenant = await TenantSelector.ResolveTenantAsync(
            httpContext,
            cancellationToken
        );
        var tenantId = persistedTenant.TenantId;

        await using var cachedTenantReference = await TenantCache.TryGetAsync(
            tenantId,
            propertyBag,
            cancellationToken
        );

        if (cachedTenantReference.IsActive)
        {
            return cachedTenantReference.AddReference();
        }

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

    /// <summary>
    /// Used to get the tenant's <see cref="IReadOnlySettingCollectionProvider"/> instance from the current HTTP request.
    /// </summary>
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
        var initialSettings = SettingSerializer.DeserializeSettings(
            initialSettingsJson,
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
        var settings = SettingSerializer.DeserializeSettings(newSettings.Value, jsonOptions);

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
        var basePath = httpRequest.PathBase;

        var tenantRoute = TenantSelector.GetTenantRoute();
        var templateBinder = TemplateBinderFactory.Create(tenantRoute);
        var tenantRouteUrl = templateBinder.BindValues(httpRequest.RouteValues);
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
        var settings = tenantSettings.Value.Collection;

        if (
            settings.TryGetValue(OpenIdSettingKeys.TenantIssuer, out var tenantIssuer)
            && !string.IsNullOrEmpty(tenantIssuer)
        )
        {
            return ValueTask.FromResult(tenantIssuer);
        }

        return ValueTask.FromResult(tenantBaseAddress.ToString());
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
