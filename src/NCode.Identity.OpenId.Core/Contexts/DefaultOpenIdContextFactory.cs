// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdContextFactory"/> abstraction.
/// </summary>
internal class DefaultOpenIdContextFactory(
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    IOpenIdPlatformProvider openIdServerProvider,
    IOpenIdTenantFactory openIdTenantFactory
) : IOpenIdContextFactory
{
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;
    private IOpenIdPlatformProvider OpenIdServerProvider { get; } = openIdServerProvider;
    private IOpenIdTenantFactory OpenIdTenantFactory { get; } = openIdTenantFactory;

    /// <inheritdoc />
    public async ValueTask<OpenIdContext> CreateForRequestAsync(
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = OpenIdEnvironmentProvider.Get();
        var openIdPlatform = await OpenIdServerProvider.GetAsync(
            openIdEnvironment,
            cancellationToken
        );

        await using var tenantReference = await OpenIdTenantFactory.CreateTenantForRequestAsync(
            httpContext,
            openIdEnvironment,
            openIdPlatform,
            openIdPlatform.PropertyBag,
            cancellationToken
        );

        return PublishContext(
            httpContext,
            mediator,
            openIdEnvironment,
            openIdPlatform,
            tenantReference
        );
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdContext> CreateForControlPlaneAsync(
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = OpenIdEnvironmentProvider.Get();
        var openIdPlatform = await OpenIdServerProvider.GetAsync(
            openIdEnvironment,
            cancellationToken
        );

        await using var tenantReference = await OpenIdTenantFactory.CreateControlPlaneTenantAsync(
            httpContext,
            openIdEnvironment,
            openIdPlatform,
            openIdPlatform.PropertyBag,
            cancellationToken
        );

        return PublishContext(
            httpContext,
            mediator,
            openIdEnvironment,
            openIdPlatform,
            tenantReference
        );
    }

    private OpenIdContext PublishContext(
        HttpContext httpContext,
        IMediator mediator,
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        AsyncSharedReferenceLease<OpenIdTenant> tenantReference
    )
    {
        var propertyBag = tenantReference.Value.PropertyBag.Clone();

        var openIdContext = CreateContext(
            httpContext,
            mediator,
            openIdEnvironment,
            openIdPlatform,
            tenantReference,
            propertyBag
        );

        httpContext.Features.Set(CreateFeature(openIdContext));
        httpContext.Response.RegisterForDisposeAsync(openIdContext);

        return openIdContext;
    }

    /// <inheritdoc />
    public virtual async ValueTask<OpenIdContext> CreateForTenantAsync(
        HttpContext httpContext,
        string tenantId,
        IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = OpenIdEnvironmentProvider.Get();
        var openIdPlatform = await OpenIdServerProvider.GetAsync(
            openIdEnvironment,
            cancellationToken
        );

        await using var tenantReference = await OpenIdTenantFactory.CreateTenantByIdAsync(
            httpContext,
            openIdEnvironment,
            openIdPlatform,
            tenantId,
            openIdPlatform.PropertyBag,
            cancellationToken
        );

        var propertyBag = tenantReference.Value.PropertyBag.Clone();

        // Intentionally not published as the ambient context feature nor registered for request disposal: this is a
        // secondary, caller-owned context for the addressed tenant, distinct from the request's own context.
        return CreateContext(
            httpContext,
            mediator,
            openIdEnvironment,
            openIdPlatform,
            tenantReference,
            propertyBag
        );
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdContext> CreateForBackgroundAsync(
        IServiceProvider serviceProvider,
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        // See ADR-1223.
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        return await CreateForTenantAsync(httpContext, tenantId, mediator, cancellationToken);
    }

    /// <summary>
    /// Factory method to create a new instance of <see cref="OpenIdContext"/>.
    /// </summary>
    protected internal virtual OpenIdContext CreateContext(
        HttpContext httpContext,
        IMediator mediator,
        OpenIdEnvironment openIdEnvironment,
        OpenIdPlatform openIdPlatform,
        AsyncSharedReferenceLease<OpenIdTenant> tenantReference,
        IPropertyBag propertyBag
    ) =>
        new DefaultOpenIdContext(
            httpContext,
            openIdEnvironment,
            openIdPlatform,
            tenantReference,
            mediator,
            propertyBag
        );

    /// <summary>
    /// Factory method to create a new instance of <see cref="IOpenIdContextFeature"/>.
    /// </summary>
    protected internal virtual IOpenIdContextFeature CreateFeature(OpenIdContext openIdContext) =>
        new DefaultOpenIdContextFeature { OpenIdContext = openIdContext };
}
