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
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a single tenant-resolution strategy (for example: static-single, dynamic-by-host, or dynamic-by-path).
/// The <see cref="ITenantResolver"/> selects the active strategy by <see cref="ProviderCode"/> and delegates to it.
/// </summary>
[PublicAPI]
public interface ITenantResolverStrategy
{
    /// <summary>
    /// Gets the <see cref="string"/> <c>Code</c> that identifies this strategy, matched against
    /// <see cref="TenantResolutionOptions.ProviderCode"/>.
    /// </summary>
    string ProviderCode { get; }

    /// <summary>
    /// Gets the <c>RoutePattern</c> for the tenant's relative base path. The pattern is empty when this strategy is
    /// not path-based.
    /// </summary>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <returns>The <c>RoutePattern</c> instance for the tenant.</returns>
    RoutePattern GetTenantRoute(IPropertyBag propertyBag);

    /// <summary>
    /// Resolves the <see cref="TenantDescriptor"/> for the current HTTP request using this strategy.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current HTTP request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the resolved
    /// <see cref="TenantDescriptor"/>.</returns>
    ValueTask<TenantDescriptor> ResolveDescriptorAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );
}
