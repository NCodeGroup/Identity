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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.PropertyBag;
using NIdentity.OpenId.Tenants.Providers;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEndpointRouteBuilder"/> abstraction.
/// </summary>
public class DefaultOpenIdEndpointRouteBuilder(
    IOpenIdTenantProviderSelector tenantProviderSelector,
    IEnumerable<IOpenIdEndpointProvider> endpointProviders
) : IOpenIdEndpointRouteBuilder
{
    private IOpenIdTenantProviderSelector TenantProviderSelector { get; } = tenantProviderSelector;
    private IEnumerable<IOpenIdEndpointProvider> EndpointProviders { get; } = endpointProviders;

    /// <inheritdoc />
    public RouteGroupBuilder MapOpenId(IEndpointRouteBuilder endpoints)
    {
        var propertyBag = new DefaultPropertyBag();

        var tenantProvider = TenantProviderSelector.SelectProvider(propertyBag);

        var tenantRoute = tenantProvider.GetTenantRoute(propertyBag);

        var openIdEndpoints = endpoints
            .MapGroup(tenantRoute)
            .WithTags("oauth2", "oidc")
            .WithGroupName("Endpoints for OAuth 2.0 & OpenID Connect");

        // other conventions may be added by the caller using the RouteGroupBuilder return value

        foreach (var endpointProvider in EndpointProviders)
        {
            endpointProvider.Map(openIdEndpoints);
        }

        return openIdEndpoints;
    }
}
