// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.Registration.AspNetCore;

/// <summary>
/// Provides the ability to map the registered endpoint-group hierarchy onto the HTTP request pipeline.
/// </summary>
[PublicAPI]
public static class EndpointRouteBuilderExtensions
{
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/> instance to map the endpoints to.</param>
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps all the endpoint groups and providers that have been registered with the service provider.
        /// </summary>
        public void MapEndpointGroups()
        {
            endpoints
                .ServiceProvider.GetRequiredService<IEndpointTreeRouteBuilder>()
                .Map(endpoints);
        }
    }
}
