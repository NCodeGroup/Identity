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
using NIdentity.OpenId.Tenants;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdEndpointContext"/> abstraction.
/// </summary>
public class DefaultOpenIdEndpointContext : OpenIdEndpointContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOpenIdEndpointContext"/> class.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance.</param>
    /// <param name="endpointDescriptor">The <see cref="OpenIdEndpointDescriptor"/> instance.</param>
    public DefaultOpenIdEndpointContext(HttpContext httpContext, OpenIdEndpointDescriptor endpointDescriptor)
    {
        HttpContext = httpContext;
        EndpointDescriptor = endpointDescriptor;
        Tenant = new DefaultOpenIdTenant(httpContext.Features);
    }

    /// <inheritdoc />
    public override HttpContext HttpContext { get; }

    /// <inheritdoc />
    public override OpenIdEndpointDescriptor EndpointDescriptor { get; }

    /// <inheritdoc />
    public override OpenIdTenant Tenant { get; }
}
