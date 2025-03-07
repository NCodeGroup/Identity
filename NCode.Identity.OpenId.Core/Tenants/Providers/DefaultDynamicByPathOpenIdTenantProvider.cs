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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdTenantProvider"/> that dynamically loads the tenant
/// configuration using a route parameter (aka path) from an HTTP request.
/// </summary>
public class DefaultDynamicByPathOpenIdTenantProvider(
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    IOpenIdServerProvider openIdServerProvider,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory
) : OpenIdTenantProvider
{
    private DynamicByPathOpenIdTenantOptions TenantOptions =>
        OpenIdOptions.Tenant.DynamicByPath ?? throw MissingTenantOptionsException();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.DynamicByPath;

    /// <inheritdoc />
    protected override PathString TenantPath => TenantOptions.TenantPath;

    /// <inheritdoc />
    protected override TemplateBinderFactory TemplateBinderFactory { get; } = templateBinderFactory;

    /// <inheritdoc />
    protected override OpenIdOptions OpenIdOptions { get; } = optionsAccessor.Value;

    /// <inheritdoc />
    protected override IOpenIdServerProvider OpenIdServerProvider { get; } = openIdServerProvider;

    /// <inheritdoc />
    protected override IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <inheritdoc />
    protected override IOpenIdTenantCache TenantCache { get; } = tenantCache;

    /// <inheritdoc />
    protected override IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; } = settingCollectionProviderFactory;

    /// <inheritdoc />
    protected override ISettingSerializer SettingSerializer { get; } = settingSerializer;

    /// <inheritdoc />
    protected override ISecretSerializer SecretSerializer { get; } = secretSerializer;

    /// <inheritdoc />
    protected override ISecretKeyCollectionProviderFactory SecretKeyCollectionProviderFactory { get; } = secretKeyCollectionProviderFactory;

    /// <inheritdoc />
    protected override ICollectionDataSourceFactory CollectionDataSourceFactory { get; } = collectionDataSourceFactory;

    /// <inheritdoc />
    protected override async ValueTask<TenantDescriptor> GetTenantDescriptorAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        var options = TenantOptions;
        var tenantRoute = GetTenantRoute(propertyBag);

        if (tenantRoute.Parameters.Count == 0)
            throw new InvalidOperationException("The TenantRoute has no parameters.");

        var httpRequest = httpContext.Request;
        var routeValues = httpRequest.RouteValues;

        if (!routeValues.TryGetValue(options.TenantIdRouteParameterName, out var routeValue))
            throw new InvalidOperationException($"The value for route parameter '{options.TenantIdRouteParameterName}' could not be found in the HTTP request.");

        if (routeValue is not string tenantId)
            throw new InvalidOperationException($"The value for route parameter '{options.TenantIdRouteParameterName}' is not a string.");

        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException($"The value for route parameter '{options.TenantIdRouteParameterName}' is empty.");

        var persistedTenant = await GetTenantByIdAsync(openIdEnvironment, openIdServer, tenantId, propertyBag, cancellationToken);
        propertyBag.Set(persistedTenant);

        return new TenantDescriptor
        {
            TenantId = persistedTenant.TenantId,
            DisplayName = persistedTenant.DisplayName,
            DomainName = persistedTenant.DomainName
        };
    }
}
