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
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Servers;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the ability to create <see cref="OpenIdTenant"/> instances from the current HTTP request.
/// </summary>
[PublicAPI]
public interface IOpenIdTenantFactory
{
    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the tenant resolved from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateTenantForRequestAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the tenant addressed by an explicit identifier, decoupled
    /// from the request's own tenant-resolution strategy. This materializes a tenant the current request is not itself
    /// addressed to — for example, a management/admin operation acting on a route-addressed tenant that must run
    /// protocol machinery (session termination, back-channel logout) in that tenant's security domain. The tenant's
    /// issuer and base address are taken from its own configuration rather than the request, so the tenant must have a
    /// configured issuer.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="tenantId">The identifier of the tenant to materialize.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateTenantByIdAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );
}
