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
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Endpoints.Discovery.Commands;
using NIdentity.OpenId.Endpoints.Discovery.Results;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Settings;

namespace NIdentity.OpenId.Endpoints.Discovery;

internal class DefaultDiscoveryEndpointHandler :
    ICommandResponseHandler<DiscoveryEndpointCommand, IOpenIdResult>,
    ICommandHandler<DiscoverMetadataCommand>
{
    private LinkGenerator LinkGenerator { get; }
    private IOpenIdEndpointCollectionProvider OpenIdEndpointCollectionProvider { get; }

    public DefaultDiscoveryEndpointHandler(
        LinkGenerator linkGenerator,
        IOpenIdEndpointCollectionProvider openIdEndpointCollectionProvider)
    {
        LinkGenerator = linkGenerator;
        OpenIdEndpointCollectionProvider = openIdEndpointCollectionProvider;
    }

    /// <inheritdoc />
    public async ValueTask<IOpenIdResult> HandleAsync(DiscoveryEndpointCommand command, CancellationToken cancellationToken)
    {
        var context = command.OpenIdContext;

        var result = new DiscoveryResult
        {
            Issuer = context.Tenant.Issuer
        };

        await context.Mediator.SendAsync(
            new DiscoverMetadataCommand(context, result.ExtensionData),
            cancellationToken
        );

        return result;
    }

    /// <inheritdoc />
    public ValueTask HandleAsync(DiscoverMetadataCommand command, CancellationToken cancellationToken)
    {
        var metadata = command.Metadata;
        var context = command.OpenIdContext;

        var showAll = context.HttpContext.Request.Query.TryGetValue("showAll", out var showAllStringValues) &&
                      StringValues.IsNullOrEmpty(showAllStringValues) ||
                      (
                          bool.TryParse(showAllStringValues, out var showAllParsed) &&
                          showAllParsed
                      );

        DiscoverSettings(metadata, context.Tenant.TenantSettings, showAll);

        DiscoverEndpoints(metadata, context.HttpContext);

        return ValueTask.CompletedTask;
    }

    private static void DiscoverSettings(IDictionary<string, object> metadata, ISettingCollection settingsCollection, bool showAll)
    {
        var settings = settingsCollection.Where(setting => showAll || setting.Descriptor.Discoverable);
        foreach (var setting in settings)
        {
            var value = setting.Descriptor.Format(setting);
            metadata[setting.Descriptor.Name] = value;
        }
    }

    private void DiscoverEndpoints(IDictionary<string, object> metadata, HttpContext httpContext)
    {
        var routeValues = new { };

        foreach (var endpoint in OpenIdEndpointCollectionProvider.OpenIdEndpoints)
        {
            if (endpoint.Metadata.GetMetadata<ISuppressDiscoveryMetadata>()?.SuppressDiscovery == true)
                continue;

            if (endpoint.Metadata.GetMetadata<ISuppressLinkGenerationMetadata>()?.SuppressLinkGeneration == true)
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
