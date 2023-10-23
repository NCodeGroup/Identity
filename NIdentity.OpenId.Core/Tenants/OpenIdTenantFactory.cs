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

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides the ability to create <see cref="OpenIdTenant"/> instances.
/// </summary>
public interface IOpenIdTenantFactory
{
    /// <summary>
    /// Factory method that creates a new <see cref="OpenIdTenant"/> instance from the specified HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<OpenIdTenant> CreateTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken);
}

/// <summary>
/// Provides a common base implementation of the <see cref="IOpenIdTenantFactory"/> abstraction.
/// </summary>
public abstract class OpenIdTenantFactory : IOpenIdTenantFactory
{
    /// <summary>
    /// Gets the route for the tenant.
    /// </summary>
    public abstract RoutePattern? TenantRoute { get; }

    /// <inheritdoc />
    public abstract ValueTask<OpenIdTenant> CreateTenantAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken);
}

/// <summary>
/// Provides a common base implementation of the <see cref="IOpenIdTenantFactory"/> abstraction.
/// </summary>
/// <typeparam name="TOptions">The type used to configure the tenant options.</typeparam>
public abstract class OpenIdTenantFactory<TOptions> : OpenIdTenantFactory
    where TOptions : CommonOpenIdTenantOptions
{
    private RoutePattern? _tenantRoute;

    /// <summary>
    /// Gets the <see cref="TemplateBinderFactory"/> used to create <see cref="TemplateBinder"/> instances.
    /// </summary>
    protected TemplateBinderFactory TemplateBinderFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdTenantFactory{TOptions}"/> class.
    /// </summary>
    protected OpenIdTenantFactory(TemplateBinderFactory templateBinderFactory)
    {
        TemplateBinderFactory = templateBinderFactory;
    }

    /// <summary>
    /// Gets a value indicating whether the current instance has been initialized.
    /// </summary>
    [MemberNotNullWhen(true, nameof(TenantOptions))]
    protected bool IsInitialized => TenantOptions is not null;

    /// <summary>
    /// Gets the <typeparamref name="TOptions"/> used to configure the current instance.
    /// </summary>
    protected TOptions? TenantOptions { get; private set; }

    /// <inheritdoc />
    public override RoutePattern? TenantRoute => _tenantRoute;

    /// <summary>
    /// Initializes the current instance with the specified <typeparamref name="TOptions"/>.
    /// </summary>
    /// <param name="tenantOptions">The <typeparamref name="TOptions"/> used to configure the current instance.</param>
    /// <exception cref="InvalidOperationException">Thrown if the current instance has already been initialized.</exception>
    public void Initialize(TOptions tenantOptions)
    {
        if (IsInitialized) throw new InvalidOperationException();

        TenantOptions = tenantOptions;

        if (!tenantOptions.TenantPath.HasValue) return;

        _tenantRoute = RoutePatternFactory.Parse(tenantOptions.TenantPath.Value);
        ValidateTenantRoute(tenantOptions, _tenantRoute);
    }

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> if the current instance has not been initialized.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the current instance has not been initialized.</exception>
    [MemberNotNull(nameof(TenantOptions))]
    protected void ThrowIfNotInitialized()
    {
        if (!IsInitialized) throw new InvalidOperationException();
    }

    /// <summary>
    /// Called to validate the <paramref name="tenantRoute"/> for the tenant.
    /// The default implementation throws an <see cref="InvalidOperationException"/> if the <paramref name="tenantRoute"/> contains any parameters.
    /// </summary>
    /// <param name="tenantOptions">The <typeparamref name="TOptions"/> used to configure the current instance.</param>
    /// <param name="tenantRoute">The <see cref="RoutePattern"/> to be validated.</param>
    protected virtual void ValidateTenantRoute(
        TOptions tenantOptions,
        RoutePattern tenantRoute)
    {
        if (tenantRoute.Parameters.Count > 0)
            // TODO: better exception/message
            throw new InvalidOperationException();
    }

    /// <summary>
    /// Gets the <c>BaseAddress</c> for a tenant.
    /// </summary>
    /// <param name="tenantOptions">The <typeparamref name="TOptions"/> used to configure the current instance.</param>
    /// <param name="tenantRoute">The <see cref="RoutePattern"/> for the tenant.</param>
    /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <c>BaseAddress</c> for the tenant.</returns>
    protected virtual ValueTask<UriDescriptor> GetBaseAddressAsync(
        TOptions tenantOptions,
        RoutePattern? tenantRoute,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var httpRequest = httpContext.Request;
        var routeValues = httpRequest.RouteValues;
        var basePath = httpRequest.PathBase;

        if (tenantRoute is not null)
        {
            var templateBinder = TemplateBinderFactory.Create(tenantRoute);
            var tenantRouteUrl = templateBinder.BindValues(routeValues);
            if (!string.IsNullOrEmpty(tenantRouteUrl))
            {
                basePath.Add(tenantRouteUrl);
            }
        }

        var baseAddress = new UriDescriptor
        {
            Scheme = httpRequest.Scheme,
            Host = httpRequest.Host,
            Path = basePath
        };

        return ValueTask.FromResult(baseAddress);
    }

    /// <summary>
    /// Gets the issuer identifier for a tenant.
    /// </summary>
    /// <param name="tenantOptions">The <typeparamref name="TOptions"/> used to configure the current instance.</param>
    /// <param name="httpContext">The <see cref="HttpContext"/> associated with the current request.</param>
    /// <param name="baseAddress">The <c>BaseAddress</c> for the tenant.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the issuer identifier for the tenant.</returns>
    protected virtual ValueTask<string> GetIssuerAsync(
        TOptions tenantOptions,
        HttpContext httpContext,
        UriDescriptor baseAddress,
        CancellationToken cancellationToken)
    {
        var issuer = tenantOptions.Issuer;
        if (string.IsNullOrEmpty(issuer))
            issuer = baseAddress.ToString();

        return ValueTask.FromResult(issuer);
    }
}
