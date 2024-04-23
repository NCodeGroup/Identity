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

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using NCode.Disposables;
using NCode.Identity;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Exceptions;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.Settings;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a common base implementation of the <see cref="IOpenIdTenantProvider"/> abstraction.
/// </summary>
public abstract class OpenIdTenantProvider(
    TemplateBinderFactory templateBinderFactory,
    OpenIdServerOptions serverOptions,
    OpenIdServer openIdServer,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    ISecretSerializer secretSerializer,
    ISecretKeyProviderFactory secretKeyProviderFactory
) : IOpenIdTenantProvider
{
    [MemberNotNullWhen(true, nameof(TenantRouteOrNull))]
    private bool TenantRouteHasValue { get; set; }

    private RoutePattern? TenantRouteOrNull { get; set; }

    /// <summary>
    /// Gets the <see cref="TemplateBinderFactory"/> used to bind route templates.
    /// </summary>
    protected TemplateBinderFactory TemplateBinderFactory { get; } = templateBinderFactory;

    /// <summary>
    /// Gets the <see cref="OpenIdServerOptions"/> used to configure the OpenID server.
    /// </summary>
    protected OpenIdServerOptions ServerOptions { get; } = serverOptions;

    /// <summary>
    /// Gets the <see cref="OpenIdServer"/> used to provide OpenID server functionality.
    /// </summary>
    protected OpenIdServer OpenIdServer { get; } = openIdServer;

    /// <summary>
    /// Gets the <see cref="IStoreManagerFactory"/> used to create <see cref="IStoreManager"/> instances.
    /// </summary>
    protected IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <summary>
    /// Gets the <see cref="IOpenIdTenantCache"/> used to cache tenant instances.
    /// </summary>
    protected IOpenIdTenantCache TenantCache { get; } = tenantCache;

    /// <summary>
    /// Gets the <see cref="ISecretSerializer"/> used to serialize/deserialize secrets.
    /// </summary>
    protected ISecretSerializer SecretSerializer { get; } = secretSerializer;

    /// <summary>
    /// Gets the <see cref="ISecretKeyProviderFactory"/> used to create <see cref="ISecretKeyProvider"/> instances.
    /// </summary>
    protected ISecretKeyProviderFactory SecretKeyProviderFactory { get; } = secretKeyProviderFactory;

    /// <inheritdoc />
    public abstract string ProviderCode { get; }

    /// <summary>
    /// Gets the relative base path for the tenant.
    /// </summary>
    protected abstract PathString TenantPath { get; }

    /// <summary>
    /// Loads the tenant using the specified <paramref name="tenantId"/> from the <see cref="ITenantStore"/>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="Tenant"/> instance.</returns>
    /// <exception cref="HttpResultException">Throw with status code 404 when then tenant could not be found.</exception>
    protected async ValueTask<Tenant> GetTenantByIdAsync(string tenantId, CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var tenant = await tenantStore.TryGetByTenantIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            throw TypedResults
                .NotFound()
                .AsException($"A tenant with identifier '{tenantId}' could not be found.");

        return tenant;
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
    public virtual async ValueTask<ISharedReference<OpenIdTenant>> GetTenantAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        var tenantDescriptor = await GetTenantDescriptorAsync(
            httpContext,
            propertyBag,
            cancellationToken);

        var tenantReference = await TenantCache.TryGetAsync(
            tenantDescriptor,
            propertyBag,
            cancellationToken);

        if (tenantReference is not null)
        {
            return tenantReference;
        }

        var tenantPropertyBag = propertyBag.Clone();

        var tenantSettings = await GetTenantSettingsAsync(
            httpContext,
            tenantPropertyBag,
            tenantDescriptor,
            cancellationToken);

        var tenantBaseAddress = await GetTenantBaseAddressAsync(
            httpContext,
            tenantPropertyBag,
            tenantDescriptor,
            tenantSettings,
            cancellationToken);

        var tenantIssuer = await GetTenantIssuerAsync(
            httpContext,
            tenantPropertyBag,
            tenantDescriptor,
            tenantSettings,
            tenantBaseAddress,
            cancellationToken);

        using var tenantSecrets = await GetTenantSecretsAsync(
            httpContext,
            tenantPropertyBag,
            tenantDescriptor,
            tenantSettings,
            tenantBaseAddress,
            tenantIssuer,
            cancellationToken);

        var newTenantReference = await CreateTenantAsync(
            httpContext,
            tenantPropertyBag,
            tenantDescriptor,
            tenantSettings,
            tenantBaseAddress,
            tenantIssuer,
            tenantSecrets,
            cancellationToken);

        try
        {
            await TenantCache.SetAsync(
                tenantDescriptor,
                newTenantReference,
                tenantPropertyBag,
                cancellationToken);
        }
        catch
        {
            newTenantReference.Dispose();
            throw;
        }

        return newTenantReference;
    }

    /// <summary>
    /// Creates and returns an <see cref="InvalidOperationException"/> instance for when the tenant options are missing.
    /// </summary>
    protected InvalidOperationException MissingTenantOptionsException() =>
        new($"The OpenIdTenant ProviderCode is '{ProviderCode}' but the corresponding options are missing.");

    /// <summary>
    /// Used to get the tenant's <see cref="TenantDescriptor"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="TenantDescriptor"/> instance.</returns>
    protected abstract ValueTask<TenantDescriptor> GetTenantDescriptorAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken);

    /// <summary>
    /// Used to get the tenant's <see cref="ISettingCollection"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="ISettingCollection"/> instance.</returns>
    protected virtual async ValueTask<ISettingCollection> GetTenantSettingsAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        TenantDescriptor tenantDescriptor,
        CancellationToken cancellationToken)
    {
        // ReSharper disable once InvertIf
        if (!propertyBag.TryGet<Tenant>(out var tenant))
        {
            tenant = await GetTenantByIdAsync(tenantDescriptor.TenantId, cancellationToken);
            propertyBag.Set(tenant);
        }

        return OpenIdServer.ServerSettings.Merge(tenant.Settings);
    }

    /// <summary>
    /// Used to get the tenant's <see cref="UriDescriptor"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="ISettingCollection"/> instance for the current tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="ISettingCollection"/> instance.</returns>
    protected virtual ValueTask<UriDescriptor> GetTenantBaseAddressAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        TenantDescriptor tenantDescriptor,
        ISettingCollection tenantSettings,
        CancellationToken cancellationToken)
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
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="ISettingCollection"/> instance for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <c>issuer identifier</c>.</returns>
    protected virtual ValueTask<string> GetTenantIssuerAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        TenantDescriptor tenantDescriptor,
        ISettingCollection tenantSettings,
        UriDescriptor tenantBaseAddress,
        CancellationToken cancellationToken)
    {
        var settingKey = new SettingKey<string>(SettingNames.TenantIssuer);
        if (tenantSettings.TryGet(settingKey, out var setting) &&
            !string.IsNullOrEmpty(setting.Value))
        {
            return ValueTask.FromResult(setting.Value);
        }

        return ValueTask.FromResult(tenantBaseAddress.ToString());
    }

    /// <summary>
    /// Used to get the tenant's <see cref="UriDescriptor"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="ISettingCollection"/> instance for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantIssuer">The <c>issuer identifier</c> for the current tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant's <see cref="ISecretKeyProvider"/> instance.</returns>
    protected virtual async ValueTask<ISharedReference<ISecretKeyProvider>> GetTenantSecretsAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        TenantDescriptor tenantDescriptor,
        ISettingCollection tenantSettings,
        UriDescriptor tenantBaseAddress,
        string tenantIssuer,
        CancellationToken cancellationToken)
    {
        // ReSharper disable once InvertIf
        if (!propertyBag.TryGet<Tenant>(out var tenant))
        {
            tenant = await GetTenantByIdAsync(tenantDescriptor.TenantId, cancellationToken);
            propertyBag.Set(tenant);
        }

        return DeserializeSecrets(
            httpContext,
            propertyBag,
            tenant.Secrets);
    }

    /// <summary>
    /// Deserializes a <see cref="DataContracts.Secret"/> collection into an <see cref="ISecretKeyProvider"/> instance.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="secrets">The collection of <see cref="DataContracts.Secret"/> instance.</param>
    /// <returns>The newly created <see cref="ISecretKeyProvider"/> instance.</returns>
    protected virtual ISharedReference<ISecretKeyProvider> DeserializeSecrets(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        IEnumerable<Secret> secrets)
    {
        // TODO: add support for a dynamic data source that re-fetches secrets from the store
        var secretKeySource = SecretSerializer.DeserializeSecrets(secrets, out _);
        var secretKeyProvider = SecretKeyProviderFactory.CreateStatic(secretKeySource);
        return SharedReference.Create(secretKeyProvider);
    }

    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="tenantDescriptor">The <see cref="TenantDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantSettings">The <see cref="ISettingCollection"/> instance for the current tenant.</param>
    /// <param name="tenantBaseAddress">The <see cref="UriDescriptor"/> instance for the current tenant.</param>
    /// <param name="tenantIssuer">The <c>issuer identifier</c> for the current tenant.</param>
    /// <param name="tenantSecrets">The <see cref="ISecretKeyProvider"/> instance for the current tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="OpenIdTenant"/> instance.</returns>
    protected virtual ValueTask<ISharedReference<OpenIdTenant>> CreateTenantAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        TenantDescriptor tenantDescriptor,
        ISettingCollection tenantSettings,
        UriDescriptor tenantBaseAddress,
        string tenantIssuer,
        ISharedReference<ISecretKeyProvider> tenantSecrets,
        CancellationToken cancellationToken)
    {
        OpenIdTenant tenant = new DefaultOpenIdTenant(
            tenantDescriptor,
            tenantIssuer,
            tenantBaseAddress,
            tenantSettings,
            tenantSecrets,
            propertyBag);

        return ValueTask.FromResult(SharedReference.Create(tenant));
    }
}
