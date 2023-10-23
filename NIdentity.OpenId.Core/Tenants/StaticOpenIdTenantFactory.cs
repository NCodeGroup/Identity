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
using Microsoft.AspNetCore.Routing.Template;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides an implementation of the <see cref="IOpenIdTenantFactory"/> abstraction that creates <see cref="OpenIdTenant"/>
/// instances using static configuration.
/// </summary>
public class StaticOpenIdTenantFactory : OpenIdTenantFactory<StaticSingleOpenIdTenantOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StaticOpenIdTenantFactory"/> class.
    /// </summary>
    public StaticOpenIdTenantFactory(TemplateBinderFactory templateBinderFactory)
        : base(templateBinderFactory)
    {
        // nothing
    }

    /// <inheritdoc />
    public override async ValueTask<OpenIdTenant> CreateTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ThrowIfNotInitialized();

        var tenantId = TenantOptions.TenantId;
        if (string.IsNullOrEmpty(tenantId))
            tenantId = StaticSingleOpenIdTenantOptions.DefaultTenantId;

        var displayName = TenantOptions.DisplayName;
        if (string.IsNullOrEmpty(displayName))
            displayName = StaticSingleOpenIdTenantOptions.DefaultDisplayName;

        var baseAddress = await GetBaseAddressAsync(
            TenantOptions,
            TenantRoute,
            httpContext,
            cancellationToken);

        var issuer = await GetIssuerAsync(
            TenantOptions,
            httpContext,
            baseAddress,
            cancellationToken);

        return new StaticOpenIdTenant(tenantId, displayName, issuer, baseAddress);
    }
}
