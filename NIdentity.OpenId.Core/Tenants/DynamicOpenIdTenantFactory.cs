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
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides a common base implementation of the <see cref="IOpenIdTenantFactory"/> abstraction
/// that loads tenant configuration from a persistent store.
/// </summary>
/// <typeparam name="TOptions"></typeparam>
public abstract class DynamicOpenIdTenantFactory<TOptions> : OpenIdTenantFactory<TOptions>
    where TOptions : CommonOpenIdTenantOptions
{
    /// <summary>
    /// Gets the <see cref="ITenantStore"/> which persists <see cref="Tenant"/> instances.
    /// </summary>
    protected ITenantStore TenantStore { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicOpenIdTenantFactory{TOptions}"/> class.
    /// </summary>
    protected DynamicOpenIdTenantFactory(TemplateBinderFactory templateBinderFactory, ITenantStore tenantStore)
        : base(templateBinderFactory)
    {
        TenantStore = tenantStore;
    }

    /// <summary>
    /// Gets the tenant identifier for a tenant.
    /// </summary>
    /// <param name="tenantOptions">The <typeparamref name="TOptions"/> used to configure the current instance.</param>
    /// <param name="tenantRoute">The <see cref="RoutePattern"/> for the tenant.</param>
    /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the tenant identifier.</returns>
    protected abstract ValueTask<string> GetTenantIdAsync(
        TOptions tenantOptions,
        RoutePattern? tenantRoute,
        HttpContext httpContext,
        CancellationToken cancellationToken);

    /// <inheritdoc />
    public override async ValueTask<OpenIdTenant> CreateTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        ThrowIfNotInitialized();

        var tenantId = await GetTenantIdAsync(
            TenantOptions,
            TenantRoute,
            httpContext,
            cancellationToken);

        if (string.IsNullOrEmpty(tenantId))
            // TODO better exception/message
            throw new InvalidOperationException();

        var tenant = await TenantStore.TryGetByTenantIdAsync(tenantId, cancellationToken);
        if (tenant is null)
            // TODO better exception/message
            throw new InvalidOperationException();

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

        return new DynamicOpenIdTenant(tenant, issuer, baseAddress);
    }
}
