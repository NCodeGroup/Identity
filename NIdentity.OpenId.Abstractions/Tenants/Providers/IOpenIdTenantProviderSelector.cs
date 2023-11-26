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

using NCode.Identity;

namespace NIdentity.OpenId.Tenants.Providers;

/// <summary>
/// Provides the ability to select an <see cref="IOpenIdTenantProvider"/> instance that the authorization server
/// will use to create <see cref="OpenIdTenant"/> instances.
/// </summary>
public interface IOpenIdTenantProviderSelector
{
    /// <summary>
    /// Gets the <see cref="IOpenIdTenantProvider"/> instance that the authorization server will use to
    /// create <see cref="OpenIdTenant"/> instances.
    /// </summary>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <returns>The <see cref="IOpenIdTenantProvider"/> instance that will be used to create <see cref="OpenIdTenant"/> instances.</returns>
    IOpenIdTenantProvider SelectProvider(IPropertyBag propertyBag);
}
