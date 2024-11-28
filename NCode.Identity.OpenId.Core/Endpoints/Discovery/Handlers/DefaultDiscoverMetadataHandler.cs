#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Mediator;

namespace NCode.Identity.OpenId.Endpoints.Discovery.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="DiscoverMetadataCommand"/> message.
/// </summary>
public class DefaultDiscoverMetadataHandler(
    EndpointDataSource endpointDataSource,
    LinkGenerator linkGenerator
) : ICommandHandler<DiscoverMetadataCommand>
{
    private EndpointDataSource EndpointDataSource { get; } = endpointDataSource;
    private LinkGenerator LinkGenerator { get; } = linkGenerator;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        DiscoverMetadataCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, metadata, showAll) = command;

        DiscoverSettings(openIdContext, metadata, showAll);

        DiscoverEndpoints(metadata, openIdContext.Http, showAll);

        return ValueTask.CompletedTask;
    }

    private static void DiscoverSettings(
        OpenIdContext openIdContext,
        IDictionary<string, object> metadata,
        bool showAll)
    {
        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var settingsToShow = settings.Where(setting => showAll || setting.Descriptor.IsDiscoverable);
        foreach (var setting in settingsToShow)
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
            var discoverable = endpoint.Metadata.GetMetadata<IOpenIdEndpointDiscoverableMetadata>()?.IsDiscoverable ?? false;
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
