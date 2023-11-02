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

using Microsoft.AspNetCore.Routing;
using NIdentity.OpenId.Endpoints.Discovery.Results;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Discovery;

internal class DiscoveryEndpointHandler : ICommandResponseHandler<DiscoveryEndpointCommand, IOpenIdResult>
{
    private LinkGenerator LinkGenerator { get; }
    private IOpenIdEndpointCollectionProvider OpenIdEndpointCollectionProvider { get; }

    public DiscoveryEndpointHandler(
        LinkGenerator linkGenerator,
        IOpenIdEndpointCollectionProvider openIdEndpointCollectionProvider)
    {
        LinkGenerator = linkGenerator;
        OpenIdEndpointCollectionProvider = openIdEndpointCollectionProvider;
    }

    public ValueTask<IOpenIdResult> HandleAsync(DiscoveryEndpointCommand command, CancellationToken cancellationToken)
    {
        var routeValues = new { };
        var openIdContext = command.OpenIdContext;
        var httpContext = openIdContext.HttpContext;

        var result = new DiscoveryResult();
        var metadata = result.Metadata;

        var settings = openIdContext.Tenant.Settings.Where(setting => setting.Descriptor.Discoverable);
        foreach (var setting in settings)
        {
            var value = setting.Descriptor.Format(setting);
            metadata[setting.Descriptor.SettingName] = value;
        }

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

        // TODO: add other metadata

        return ValueTask.FromResult<IOpenIdResult>(result);
    }
}
