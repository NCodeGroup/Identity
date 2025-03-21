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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Servers;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants.Providers;

/// <summary>
/// Provides the ability to get the <see cref="OpenIdTenant"/> instance from an HTTP request.
/// </summary>
[PublicAPI]
public interface IOpenIdTenantProvider
{
    /// <summary>
    /// Gets the <see cref="string"/> <c>Code</c> for the current provider.
    /// </summary>
    string ProviderCode { get; }

    /// <summary>
    /// Gets the <see cref="T:RoutePattern"/> for the tenant's relative base path.
    /// </summary>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <returns>The <see cref="T:RoutePattern"/> instance for the tenant.</returns>
    RoutePattern GetTenantRoute(IPropertyBag propertyBag);

    /// <summary>
    /// Gets the <see cref="OpenIdTenant"/> instance from the specified HTTP request.
    /// This method uses <see cref="AsyncSharedReferenceLease{T}"/> so that the tenant instance can be cached and shared
    /// across multiple requests.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> GetTenantAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );
}
