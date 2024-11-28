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
using NCode.Identity.OpenId.Options;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdTenantProvider"/> that dynamically loads the tenant
/// configuration using a route parameter (aka path) from a HTTP request.
/// </summary>
public class DefaultDynamicByPathOpenIdTenantProvider(
    TemplateBinderFactory templateBinderFactory,
    IOptions<OpenIdServerOptions> serverOptionsAccessor,
    OpenIdServer openIdServer,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdTenantCache tenantCache,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer,
    ISecretKeyCollectionProviderFactory secretKeyCollectionProviderFactory,
    ICollectionDataSourceFactory collectionDataSourceFactory,
    IReadOnlySettingCollectionProviderFactory settingCollectionProviderFactory
) : OpenIdTenantProvider(
    templateBinderFactory,
    serverOptionsAccessor.Value,
    openIdServer,
    storeManagerFactory,
    tenantCache,
    settingSerializer,
    secretSerializer,
    secretKeyCollectionProviderFactory,
    collectionDataSourceFactory
)
{
    private DynamicByPathOpenIdTenantOptions TenantOptions =>
        ServerOptions.Tenant.DynamicByPath ?? throw MissingTenantOptionsException();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.DynamicByPath;

    /// <inheritdoc />
    protected override PathString TenantPath => TenantOptions.TenantPath;

    /// <inheritdoc />
    protected override IReadOnlySettingCollectionProviderFactory SettingCollectionProviderFactory { get; } = settingCollectionProviderFactory;

    /// <inheritdoc />
    protected override async ValueTask<TenantDescriptor> GetTenantDescriptorAsync(
        HttpContext httpContext,
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

        var persistedTenant = await GetTenantByIdAsync(tenantId, propertyBag, cancellationToken);

        return new TenantDescriptor
        {
            TenantId = persistedTenant.TenantId,
            DisplayName = persistedTenant.DisplayName,
            DomainName = persistedTenant.DomainName
        };
    }
}
