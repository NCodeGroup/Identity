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

using Microsoft.AspNetCore.Http;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tenants.Providers;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdTenantFactory"/> abstraction.
/// </summary>
public class DefaultOpenIdTenantFactory(
    IOpenIdTenantProviderSelector tenantProviderSelector
) : IOpenIdTenantFactory
{
    private IOpenIdTenantProviderSelector TenantProviderSelector { get; } = tenantProviderSelector;

    /// <inheritdoc />
    public async ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateTenantAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    )
    {
        var tenantProvider = TenantProviderSelector.SelectProvider(propertyBag);

        // no need to add ref since we return immediately
        var tenantReference = await tenantProvider.GetTenantAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
            propertyBag,
            cancellationToken
        );

        return tenantReference;
    }
}
