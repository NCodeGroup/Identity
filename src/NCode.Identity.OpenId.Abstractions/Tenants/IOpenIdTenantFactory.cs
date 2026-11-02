// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Servers;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Provides the ability to create <see cref="OpenIdTenant"/> instances from the current HTTP request.
/// </summary>
[PublicAPI]
public interface IOpenIdTenantFactory
{
    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the tenant resolved from the current HTTP request.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateTenantForRequestAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the control-plane tenant, which
    /// <see cref="ITenantResolver.ResolveControlPlaneTenantAsync"/> resolves whatever tenant the current HTTP request
    /// addresses. Like the request's own tenant, it is seeded when it is materialized and shared through the tenant cache.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateControlPlaneTenantAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Creates a new <see cref="OpenIdTenant"/> instance for the tenant addressed by an explicit identifier, decoupled
    /// from the request's own tenant-resolution strategy. This materializes a tenant the current request is not itself
    /// addressed to — for example, a management/admin operation acting on a route-addressed tenant that must run
    /// protocol machinery (session termination, back-channel logout) in that tenant's security domain. Like every
    /// materialized tenant, its issuer and base address are taken from its own issuer, never from the request, so the
    /// tenant must have one.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> instance associated with the current request.</param>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="openIdServer">The <see cref="OpenIdServer"/> instance associated with the current request.</param>
    /// <param name="tenantId">The identifier of the tenant to materialize.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the newly created <see cref="OpenIdTenant"/> instance.</returns>
    ValueTask<AsyncSharedReferenceLease<OpenIdTenant>> CreateTenantByIdAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        string tenantId,
        IPropertyBag propertyBag,
        CancellationToken cancellationToken
    );
}
