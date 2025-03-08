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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Endpoints.Discovery.Results;
using NCode.Identity.OpenId.Mediator;

namespace NCode.Identity.OpenId.Endpoints.Discovery;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the discovery endpoint.
/// </summary>
public class DefaultDiscoveryEndpointHandler(
    IOpenIdContextFactory contextFactory
) : IOpenIdEndpointProvider
{
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;

    /// <inheritdoc />
    public RouteHandlerBuilder Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapGet(OpenIdConstants.EndpointPaths.Discovery, HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Discovery)
        .WithOpenIdDiscoverable();

    private async ValueTask<JsonHttpResult<DiscoveryResult>> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        [FromQuery] bool? showAll,
        CancellationToken cancellationToken)
    {
        var openIdContext = await ContextFactory.CreateAsync(
            httpContext,
            mediator,
            cancellationToken
        );

        var openIdEnvironment = openIdContext.Environment;

        var result = new DiscoveryResult
        {
            Issuer = openIdContext.Tenant.Issuer
        };

        await mediator.SendAsync(
            new DiscoverMetadataCommand(openIdContext, result.Metadata, showAll ?? false),
            cancellationToken
        );

        return TypedResults.Json(result, openIdEnvironment.JsonSerializerOptions);
    }
}
