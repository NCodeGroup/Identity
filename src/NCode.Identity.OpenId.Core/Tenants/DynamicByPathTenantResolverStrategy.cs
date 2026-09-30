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
using Microsoft.Extensions.Options;
using NCode.Persistence.Stores;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a tenant-resolution strategy that resolves the tenant dynamically from a route parameter (aka path)
/// of the request.
/// </summary>
internal class DynamicByPathTenantResolverStrategy(
    IStoreManagerFactory storeManagerFactory,
    IOptions<TenantResolutionOptions> optionsAccessor
) : TenantResolverStrategy(storeManagerFactory)
{
    private DynamicByPathTenantOptions Options =>
        optionsAccessor.Value.DynamicByPath ?? new DynamicByPathTenantOptions();

    /// <inheritdoc />
    public override string ProviderCode => OpenIdConstants.TenantProviderCodes.DynamicByPath;

    /// <inheritdoc />
    protected override PathString TenantPath => Options.TenantPath;

    /// <inheritdoc />
    public override async ValueTask<TenantDescriptor> ResolveDescriptorAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var options = Options;
        var tenantRoute = GetTenantRoute(propertyBag);

        if (tenantRoute.Parameters.Count == 0)
            throw new InvalidOperationException("The TenantRoute has no parameters.");

        var routeValues = httpContext.Request.RouteValues;

        if (!routeValues.TryGetValue(options.TenantIdRouteParameterName, out var routeValue))
            throw new InvalidOperationException(
                $"The value for route parameter '{options.TenantIdRouteParameterName}' could not be found in the HTTP request."
            );

        if (routeValue is not string tenantId)
            throw new InvalidOperationException(
                $"The value for route parameter '{options.TenantIdRouteParameterName}' is not a string."
            );

        if (string.IsNullOrEmpty(tenantId))
            throw new InvalidOperationException(
                $"The value for route parameter '{options.TenantIdRouteParameterName}' is empty."
            );

        var persistedTenant = await GetTenantByIdAsync(tenantId, propertyBag, cancellationToken);

        return CreateDescriptor(persistedTenant);
    }
}
