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
using Microsoft.AspNetCore.Http.Features;
using NIdentity.OpenId.Features;
using NIdentity.OpenId.Tenants;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdEndpointContext"/> abstraction.
/// </summary>
public class DefaultOpenIdEndpointContext : OpenIdEndpointContext
{
    private readonly HttpContext _httpContext;

    private FeatureReferences<FeatureInterfaces> _features;
    private static readonly Func<IFeatureCollection, IOpenIdTenantFeature?> NullTenantFeature = _ => null;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOpenIdEndpointContext"/> class.
    /// </summary>
    public DefaultOpenIdEndpointContext(HttpContext httpContext, OpenIdEndpointDescriptor endpointDescriptor)
    {
        _httpContext = httpContext;
        EndpointDescriptor = endpointDescriptor;

        _features.Initalize(_httpContext.Features);
    }

    private IOpenIdTenantFeature? TenantFeatureOrNull =>
        _features.Fetch(ref _features.Cache.Tenant, NullTenantFeature);

    /// <inheritdoc />
    public override IOpenIdTenant Tenant
    {
        get
        {
            var feature = TenantFeatureOrNull;
            if (feature == null)
            {
                throw new InvalidOperationException("Tenant has not been configured for this application.");
            }

            return feature.Tenant;
        }
    }

    /// <inheritdoc />
    public override HttpContext HttpContext => _httpContext;

    /// <inheritdoc />
    public override OpenIdEndpointDescriptor EndpointDescriptor { get; }

    private struct FeatureInterfaces
    {
        public IOpenIdTenantFeature? Tenant;
    }
}
