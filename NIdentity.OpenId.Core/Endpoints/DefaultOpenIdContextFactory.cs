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
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.Tenants;

namespace NIdentity.OpenId.Endpoints;

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
        var propertyBag = OpenIdServer.PropertyBag.Clone();

        var openIdTenant = await OpenIdTenantFactory.CreateTenantAsync(
            httpContext,
            propertyBag,
            cancellationToken);

        var openIdContext = new DefaultOpenIdContext(
            httpContext,
            OpenIdServer,
            openIdTenant,
            mediator,
            propertyBag);

        return openIdContext;
    }
}
