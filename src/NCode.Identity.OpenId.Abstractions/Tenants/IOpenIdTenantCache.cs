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
using NCode.Disposables;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides an abstraction for caching <see cref="OpenIdTenant"/> instances.
/// </summary>
[PublicAPI]
public interface IOpenIdTenantCache
{
    /// <summary>
    /// Attempts to get an <see cref="OpenIdTenant"/> instance from the cache using the specified <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to get from the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> TryGetAsync(
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Sets an <see cref="OpenIdTenant"/> instance in the cache using the specified <paramref name="tenantId"/>.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to set in the cache.</param>
    /// <param name="tenant">The <see cref="OpenIdTenant"/> instance to set in the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetAsync(
        string tenantId,
        AsyncSharedReferenceLease<OpenIdTenant> tenant,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes the <see cref="OpenIdTenant"/> instance cached for the specified <paramref name="tenantId"/>, if any, so the
    /// next request for the tenant materializes it again from the store. A lease already handed out stays usable until
    /// its holder releases it.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant to remove from the cache.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveAsync(
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes every cached <see cref="OpenIdTenant"/> instance, so the next request for any tenant materializes it
    /// again from the store. A lease already handed out stays usable until its holder releases it.
    /// </summary>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> instance that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ClearAsync(IPropertyBag propertyBag, CancellationToken cancellationToken);
}
