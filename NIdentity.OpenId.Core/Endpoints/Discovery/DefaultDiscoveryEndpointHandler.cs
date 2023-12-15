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
using NIdentity.OpenId.Endpoints.Discovery.Commands;
using NIdentity.OpenId.Endpoints.Discovery.Results;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Servers;
using NIdentity.OpenId.Settings;

namespace NIdentity.OpenId.Endpoints.Discovery;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the discovery endpoint.
/// </summary>
public class DefaultDiscoveryEndpointHandler(
    EndpointDataSource endpointDataSource,
    LinkGenerator linkGenerator,
    IOpenIdContextFactory contextFactory,
    OpenIdServer openIdServer
) : IOpenIdEndpointProvider, ICommandHandler<DiscoverMetadataCommand>
{
    private EndpointDataSource EndpointDataSource { get; } = endpointDataSource;
    private LinkGenerator LinkGenerator { get; } = linkGenerator;
    private IOpenIdContextFactory ContextFactory { get; } = contextFactory;
    private OpenIdServer OpenIdServer { get; } = openIdServer;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints) => endpoints
        .MapGet(OpenIdConstants.EndpointPaths.Discovery, HandleRouteAsync)
        .WithName(OpenIdConstants.EndpointNames.Discovery)
        .OpenIdDiscoverable();

    private async ValueTask<JsonHttpResult<DiscoveryResult>> HandleRouteAsync(
        HttpContext httpContext,
        [FromServices] IMediator mediator,
        [FromQuery] bool? showAll,
        CancellationToken cancellationToken)
    {
        var openIdContext = await ContextFactory.CreateAsync(
            httpContext,
            mediator,
            cancellationToken);

        var result = new DiscoveryResult
        {
            Issuer = openIdContext.Tenant.Issuer
        };

        await mediator.SendAsync(
            new DiscoverMetadataCommand(openIdContext, result.Metadata, showAll ?? false),
            cancellationToken
        );

        return TypedResults.Json(result, OpenIdServer.JsonSerializerOptions);
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(
        DiscoverMetadataCommand command,
        CancellationToken cancellationToken)
    {
        var (context, metadata, showAll) = command;

        DiscoverSettings(metadata, context.Tenant.TenantSettings, showAll);

        DiscoverEndpoints(metadata, context.Http, showAll);

        return ValueTask.CompletedTask;
    }

    private static void DiscoverSettings(
        IDictionary<string, object> metadata,
        ISettingCollection settingsCollection,
        bool showAll)
    {
        var settings = settingsCollection.Where(setting => showAll || setting.Descriptor.Discoverable);
        foreach (var setting in settings)
        {
            var value = setting.Descriptor.Format(setting);
            metadata[setting.Descriptor.Name] = value;
        }
    }

    private void DiscoverEndpoints(
        IDictionary<string, object> metadata,
        HttpContext httpContext,
        bool showAll)
    {
        var routeValues = new { };

        foreach (var endpoint in EndpointDataSource.Endpoints)
        {
            var discoverable = endpoint.Metadata.GetMetadata<IOpenIdEndpointDiscoverableMetadata>()?.Discoverable ?? false;
            if (!discoverable && !showAll)
                continue;

            var suppressLinkGeneration = endpoint.Metadata.GetMetadata<ISuppressLinkGenerationMetadata>()?.SuppressLinkGeneration ?? false;
            if (suppressLinkGeneration)
                continue;

            var endpointName = endpoint.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName;
            if (string.IsNullOrEmpty(endpointName))
                continue;

            var endpointUrl = LinkGenerator.GetUriByName(httpContext, endpointName, routeValues);
            if (string.IsNullOrEmpty(endpointUrl))
                continue;

            metadata[endpointName] = endpointUrl;
        }
    }
}
