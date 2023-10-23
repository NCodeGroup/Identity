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
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides an implementation of the <see cref="IOpenIdTenantFactory"/> abstraction that creates <see cref="OpenIdTenant"/>
/// instances by using a route pattern to match the tenant identifier.
/// </summary>
public class DynamicByPathOpenIdTenantFactory : DynamicOpenIdTenantFactory<DynamicByPathOpenIdTenantOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicByPathOpenIdTenantFactory"/> class.
    /// </summary>
    public DynamicByPathOpenIdTenantFactory(TemplateBinderFactory templateBinderFactory, ITenantStore tenantStore)
        : base(templateBinderFactory, tenantStore)
    {
        // nothing
    }

    /// <inheritdoc />
    protected override void ValidateTenantRoute(
        DynamicByPathOpenIdTenantOptions tenantOptions,
        RoutePattern tenantRoute)
    {
        // TODO: better exception/message

        if (tenantRoute.Parameters.Count == 0)
            throw new InvalidOperationException();

        if (tenantRoute.Parameters.Count > 1)
            throw new InvalidOperationException();

        if (tenantRoute.Parameters[0].Name != tenantOptions.TenantIdRouteParameterName)
            throw new InvalidOperationException();
    }

    /// <inheritdoc />
    protected override ValueTask<string> GetTenantIdAsync(
        DynamicByPathOpenIdTenantOptions tenantOptions,
        RoutePattern? tenantRoute,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var httpRequest = httpContext.Request;
        var routeValues = httpRequest.RouteValues;

        if (!routeValues.TryGetValue(tenantOptions.TenantIdRouteParameterName, out var routeValue))
            // TODO better exception/message
            throw new InvalidOperationException();

        if (routeValue is not string tenantId)
            // TODO better exception/message
            throw new InvalidOperationException();

        return ValueTask.FromResult(tenantId);
    }
}
