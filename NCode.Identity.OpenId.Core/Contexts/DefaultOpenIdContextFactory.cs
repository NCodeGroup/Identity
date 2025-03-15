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
using Microsoft.AspNetCore.Http;
using NCode.Disposables;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdContextFactory"/> abstraction.
/// </summary>
[PublicAPI]
public class DefaultOpenIdContextFactory(
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    IOpenIdServerProvider openIdServerProvider,
    IOpenIdTenantFactory openIdTenantFactory
) : IOpenIdContextFactory
{
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } = openIdEnvironmentProvider;
    private IOpenIdServerProvider OpenIdServerProvider { get; } = openIdServerProvider;
    private IOpenIdTenantFactory OpenIdTenantFactory { get; } = openIdTenantFactory;

    /// <inheritdoc />
    public async ValueTask<OpenIdContext> CreateAsync(
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = OpenIdEnvironmentProvider.Get();
        var openIdServer = await OpenIdServerProvider.GetAsync(openIdEnvironment, cancellationToken);

        await using var tenantReference = await OpenIdTenantFactory.CreateTenantAsync(
            httpContext,
            openIdEnvironment,
            openIdServer,
            openIdServer.PropertyBag,
            cancellationToken
        );

        var propertyBag = tenantReference.Value.PropertyBag.Clone();

        var openIdContext = CreateContext(
            httpContext,
            mediator,
            openIdEnvironment,
            openIdServer,
            tenantReference,
            propertyBag
        );

        httpContext.Features.Set(CreateFeature(openIdContext));
        httpContext.Response.RegisterForDisposeAsync(openIdContext);

        return openIdContext;
    }

    /// <summary>
    /// Factory method to create a new instance of <see cref="OpenIdContext"/>.
    /// </summary>
    protected internal virtual OpenIdContext CreateContext(
        HttpContext httpContext,
        IMediator mediator,
        OpenIdEnvironment openIdEnvironment,
        OpenIdServer openIdServer,
        AsyncSharedReferenceLease<OpenIdTenant> tenantReference,
        IPropertyBag propertyBag
    ) =>
        new DefaultOpenIdContext(
            httpContext,
            openIdEnvironment,
            openIdServer,
            tenantReference,
            mediator,
            propertyBag
        );

    /// <summary>
    /// Factory method to create a new instance of <see cref="IOpenIdContextFeature"/>.
    /// </summary>
    protected internal virtual IOpenIdContextFeature CreateFeature(
        OpenIdContext openIdContext
    ) =>
        new OpenIdContextFeature { OpenIdContext = openIdContext };
}
