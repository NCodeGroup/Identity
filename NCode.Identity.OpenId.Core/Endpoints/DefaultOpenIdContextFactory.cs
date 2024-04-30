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
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.OpenId.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdContextFactory"/> abstraction.
/// </summary>
public class DefaultOpenIdContextFactory(
    OpenIdServer openIdServer,
    IOpenIdTenantFactory openIdTenantFactory
) : IOpenIdContextFactory
{
    private OpenIdServer OpenIdServer { get; } = openIdServer;
    private IOpenIdTenantFactory OpenIdTenantFactory { get; } = openIdTenantFactory;

    /// <inheritdoc />
    public async ValueTask<OpenIdContext> CreateAsync(
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        await using var tenantReference = await OpenIdTenantFactory.CreateTenantAsync(
            httpContext,
            OpenIdServer.PropertyBag,
            cancellationToken);

        var propertyBag = tenantReference.Value.PropertyBag.Clone();

        var openIdContext = new DefaultOpenIdContext(
            httpContext,
            OpenIdServer,
            tenantReference,
            mediator,
            propertyBag);

        httpContext.Response.RegisterForDisposeAsync(openIdContext);

        return openIdContext;
    }
}
