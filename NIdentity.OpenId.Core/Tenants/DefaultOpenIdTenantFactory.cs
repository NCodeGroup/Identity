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
using NCode.PropertyBag;
using NIdentity.OpenId.Tenants.Providers;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdTenantFactory"/> abstraction.
/// </summary>
public class DefaultOpenIdTenantFactory(
    IOpenIdTenantProviderSelector tenantProviderSelector
) : IOpenIdTenantFactory
{
    private IOpenIdTenantProviderSelector TenantProviderSelector { get; } = tenantProviderSelector;

    /// <inheritdoc />
    public async ValueTask<ISharedReference<OpenIdTenant>> CreateTenantAsync(
        HttpContext httpContext,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken)
    {
        var tenantProvider = TenantProviderSelector.SelectProvider(propertyBag);

        var tenantReference = await tenantProvider.GetTenantAsync(
            httpContext,
            propertyBag,
            cancellationToken);

        return tenantReference;
    }
}
