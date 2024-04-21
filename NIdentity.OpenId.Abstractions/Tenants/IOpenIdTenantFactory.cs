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
using NCode.Disposables;
using NCode.Identity;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides the ability to create <see cref="OpenIdTenant"/> instances from the current HTTP request.
/// </summary>
public interface IOpenIdTenantFactory
{
    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created
    /// <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<ISharedReference<OpenIdTenant>> CreateTenantAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken);
}
