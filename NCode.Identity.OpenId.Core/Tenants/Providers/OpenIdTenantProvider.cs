﻿#region Copyright Preamble

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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using NCode.Collections.Providers;
using NCode.Collections.Providers.PeriodicPolling;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Models;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a common base implementation of the <see cref="IOpenIdTenantProvider"/> abstraction.
/// </summary>
[PublicAPI]
public abstract class OpenIdTenantProvider : IOpenIdTenantProvider
{
    [MemberNotNullWhen(true, nameof(TenantRouteOrNull))]
    private bool TenantRouteHasValue { get; set; }

    private RoutePattern? TenantRouteOrNull { get; set; }

    /// <inheritdoc />
    public abstract string ProviderCode { get; }

    /// <summary>
    /// Gets the relative base path for the tenant.
    /// </summary>
    protected abstract PathString TenantPath { get; }

    /// <summary>
    /// Gets the <see cref="TemplateBinderFactory"/> used to bind route templates.
    /// </summary>
    protected abstract TemplateBinderFactory TemplateBinderFactory { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdOptions"/> used to configure OpenID.
    /// </summary>
    protected abstract OpenIdOptions OpenIdOptions { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdServerProvider"/> used to get the current <see cref="OpenIdServer"/> instance.
    /// </summary>
    protected abstract IOpenIdServerProvider OpenIdServerProvider { get; }

    /// <summary>
    /// Gets the <see cref="IStoreManagerFactory"/> used to create <see cref="IStoreManager"/> instances.
    /// </summary>
    protected abstract IStoreManagerFactory StoreManagerFactory { get; }

    /// <summary>
    /// Gets the <see cref="IOpenIdTenantCache"/> used to cache tenant instances.
    /// </summary>
    protected abstract IOpenIdTenantCache TenantCache { get; }

    /// <summary>
    /// Gets the <see cref="IReadOnlySettingCollectionProviderFactory"/> used to create <see cref="IReadOnlySettingCollectionProvider"/> instances.
    /// </summary>
    protected abstract IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; }

    /// <summary>
    /// Gets the <see cref="ISettingSerializer"/> used to serialize/deserialize settings.
    /// </summary>
    protected abstract ISettingSerializer SettingSerializer { get; }

    /// <summary>
    /// Gets the <see cref="ISecretSerializer"/> used to serialize/deserialize secrets.
    /// </summary>
    protected abstract ISecretSerializer SecretSerializer { get; }

    /// <summary>
    /// Gets the <see cref="ISecretKeyCollectionProviderFactory"/> used to create <see cref="ISecretKeyCollectionProvider"/> instances.
    /// </summary>
    protected abstract ISecretKeyCollectionProviderFactory SecretKeyCollectionProviderFactory { get; }

    /// <summary>
    /// Gets the <see cref="ICollectionDataSourceFactory"/> used to create <see cref="ICollectionDataSource{T}"/> instances.
    /// </summary>
    protected abstract ICollectionDataSourceFactory CollectionDataSourceFactory { get; }

    /// <summary>
    /// Attempts to load a <see cref="PersistedTenant"/> using the specified <paramref name="tenantId"/> from the <see cref="ITenantStore"/>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedTenant"/> instance.</returns>
    protected virtual async ValueTask<PersistedTenant?> TryGetTenantByIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ITenantStore>();

        var persistedTenant = await store.TryGetByTenantIdAsync(tenantId, cancellationToken);
        return persistedTenant;
    }

    /// <summary>
    /// Loads a <see cref="PersistedTenant"/> using the specified <paramref name="tenantId"/> from the <see cref="ITenantStore"/>.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedTenant"/> instance.</returns>
    /// <exception cref="HttpResultException">Throw with status code 404 when then tenant could not be found.</exception>
    protected async ValueTask<PersistedTenant> GetTenantByIdAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        if (propertyBag.TryGet<PersistedTenant>(out var persistedTenant, tenantId) && persistedTenant.TenantId == tenantId)
            return persistedTenant;

        persistedTenant = await TryGetTenantByIdAsync(tenantId, cancellationToken);
        propertyBag.Set(persistedTenant);

        var errorFactory = openIdEnvironment.ErrorFactory;

        if (persistedTenant == null)
            throw errorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithStatusCode(StatusCodes.Status404NotFound)
                .WithDescription($"The tenant with identifier '{tenantId}' could not be found.")
                .AsException();

        if (persistedTenant.IsDisabled)
            throw errorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithStatusCode(StatusCodes.Status404NotFound)
                .WithDescription($"The tenant with identifier '{tenantId}' is disabled.")
                .AsException();

        propertyBag.Set(persistedTenant, tenantId);

        return persistedTenant;
    }

    /// <inheritdoc />
    public virtual RoutePattern GetTenantRoute(IPropertyBag propertyBag)
    {
        if (TenantRouteHasValue)
            return TenantRouteOrNull;

        var tenantPath = TenantPath;
        var tenantRoute = RoutePatternFactory.Parse(tenantPath.Value ?? string.Empty);

        TenantRouteOrNull = tenantRoute;
        TenantRouteHasValue = true;

        return tenantRoute;
    }

    /// <inheritdoc />
    public virtual async ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> GetTenantAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var tenantDescriptor = await GetTenantDescriptorAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
            propertyBag,
            cancellationToken
        );

        await using var cachedTenantReference = await TenantCache.TryGetAsync(
            tenantDescriptor,
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
            tenantDescriptor,
            tenantPropertyBag,
            cancellationToken
        );

        var tenantBaseAddress = await GetTenantBaseAddressAsync(
            httpContext,
            tenantDescriptor,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        var tenantIssuer = await GetTenantIssuerAsync(
            httpContext,
            tenantDescriptor,
            tenantBaseAddress,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        await using var tenantSecrets = await GetTenantSecretsAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
            tenantDescriptor,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantPropertyBag,
            cancellationToken
        );

        await using var newTenantReference = await CreateTenantAsync(
            httpContext,
            tenantDescriptor,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantSecrets,
            tenantPropertyBag,
            cancellationToken
        );

        await TenantCache.SetAsync(
            tenantDescriptor,
            newTenantReference,
            tenantPropertyBag,
            cancellationToken
        );

        return newTenantReference.AddReference();
    }

    /// <summary>
    /// Creates and returns an <see cref="InvalidOperationException"/> instance for when the tenant options are missing.
    /// </summary>
    protected InvalidOperationException MissingTenantOptionsException() =>
        new($"The OpenIdTenant ProviderCode is '{ProviderCode}' but the corresponding options are missing.");

    /// <summary>
    /// Used to get the tenant's <see cref="TenantDescriptor"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="TenantDescriptor"/> instance.</returns>
    protected abstract ValueTask<TenantDescriptor> GetTenantDescriptorAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Used to get the tenant's <see cref="IReadOnlySettingCollectionProvider"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="IReadOnlySettingCollectionProvider"/> instance.</returns>
    protected virtual async ValueTask<AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider>> GetTenantSettingsAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        TenantDescriptor tenantDescriptor,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var tenantId = tenantDescriptor.TenantId;
        var persistedTenant = await GetTenantByIdAsync(
            openIdEnvironment,
            openIdServer,
            tenantId,
            propertyBag,
            cancellationToken
        );

        var initialSettingsJson = persistedTenant.SettingsState.Value;
        var initialSettings = SettingSerializer.DeserializeSettings(openIdEnvironment, initialSettingsJson);

        var periodicPollingSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            new RefreshSettingsState(openIdEnvironment, openIdServer, persistedTenant),
            initialSettings,
            OpenIdOptions.Tenant.SettingsPeriodicRefreshInterval,
            RefreshSettingsAsync
        );

        var dataSources = new List<ICollectionDataSource<Setting>>
        {
            openIdServer.SettingsProvider.AsDataSource(),
            periodicPollingSource
        };

        var provider = SettingCollectionProviderFactory.Create(
            dataSources,
            owns: true
        );

        return provider.AsSharedReference();
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
        var prevSettingsState = persistedTenant.SettingsState;

        var (newSettingsJson, newConcurrencyToken) = await store.GetSettingsAsync(
            tenantId,
            prevSettingsState,
            cancellationToken
        );

        var prevConcurrencyToken = prevSettingsState.ConcurrencyToken;
        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<Setting>();

        var newSettings = SettingSerializer.DeserializeSettings(openIdEnvironment, newSettingsJson);

        // update the state after successfully deserializing the settings
        persistedTenant.SettingsState = ConcurrentStateFactory.Create(newSettingsJson, newConcurrencyToken);

        return RefreshCollectionResultFactory.Changed(newSettings);
    }

    /// <summary>
    /// Used to get the tenant's <see cref="UriDescriptor"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="ISettingCollection"/> instance.</returns>
    protected virtual ValueTask<UriDescriptor> GetTenantBaseAddressAsync(
        HttpContext httpContext,
        TenantDescriptor tenantDescriptor,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var httpRequest = httpContext.Request;
        var basePath = httpRequest.PathBase;

        var tenantRoute = GetTenantRoute(propertyBag);
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
            Path = basePath
        };

        return ValueTask.FromResult(baseAddress);
    }

    /// <summary>
    /// Used to get the tenant's <c>issuer identifier</c> from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <c>issuer identifier</c>.</returns>
    protected virtual ValueTask<string> GetTenantIssuerAsync(
        HttpContext httpContext,
        TenantDescriptor tenantDescriptor,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var settings = tenantSettings.Value.Collection;

        if (settings.TryGetValue(SettingKeys.TenantIssuer, out var tenantIssuer) && !string.IsNullOrEmpty(tenantIssuer))
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
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantIssuer">The <c>issuer identifier</c> for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="ISecretKeyCollectionProvider"/> instance.</returns>
    protected virtual async ValueTask<AsyncSharedReferenceLease<ISecretKeyCollectionProvider>> GetTenantSecretsAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        TenantDescriptor tenantDescriptor,
        string tenantIssuer,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var tenantId = tenantDescriptor.TenantId;
        var persistedTenant = await GetTenantByIdAsync(
            openIdEnvironment,
            openIdServer,
            tenantId,
            propertyBag,
            cancellationToken
        );

        var refreshInterval = OpenIdOptions.Tenant.SecretsPeriodicRefreshInterval;
        var initialCollection = SecretSerializer.DeserializeSecrets(persistedTenant.SecretsState.Value, out _);

        var dataSource = CollectionDataSourceFactory.CreatePeriodicPolling(
            persistedTenant,
            initialCollection,
            refreshInterval,
            RefreshSecretsAsync
        );

        var provider = SecretKeyCollectionProviderFactory.Create(dataSource, owns: true);

        return provider.AsSharedReference();
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
        var prevPersistedSecrets = persistedTenant.SecretsState;

        var (newPersistedSecrets, newConcurrencyToken) = await store.GetSecretsAsync(
            tenantId,
            prevPersistedSecrets,
            cancellationToken
        );

        var prevConcurrencyToken = prevPersistedSecrets.ConcurrencyToken;
        if (string.Equals(prevConcurrencyToken, newConcurrencyToken, StringComparison.Ordinal))
            return RefreshCollectionResultFactory.Unchanged<SecretKey>();

        var newSecrets = SecretSerializer.DeserializeSecrets(newPersistedSecrets, out _);

        // update the state after successfully deserializing the secrets
        persistedTenant.SecretsState = ConcurrentStateFactory.Create(newPersistedSecrets, newConcurrencyToken);

        return RefreshCollectionResultFactory.Changed(newSecrets);
    }

    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantIssuer">The <c>issuer identifier</c> for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="IReadOnlySettingCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="tenantSecrets">The <see cref="ISecretKeyCollectionProvider"/> instance for the current tenant.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="OpenIdTenant"/> instance.</returns>
    protected virtual ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateTenantAsync(
        HttpContext httpContext,
        TenantDescriptor tenantDescriptor,
        string tenantIssuer,
        UriDescriptor tenantBaseAddress,
        AsyncSharedReferenceLease<IReadOnlySettingCollectionProvider> tenantSettings,
        AsyncSharedReferenceLease<ISecretKeyCollectionProvider> tenantSecrets,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        OpenIdTenant tenant = new DefaultOpenIdTenant(
            tenantDescriptor,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantSecrets,
            propertyBag
        );

        return ValueTask.FromResult(tenant.AsSharedReference());
    }
}
