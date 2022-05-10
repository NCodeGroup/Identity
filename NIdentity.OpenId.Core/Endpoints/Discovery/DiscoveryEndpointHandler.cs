#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Requests.Discovery;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.Endpoints.Discovery;

internal class DiscoveryEndpointHandler : IRequestResponseHandler<DiscoveryEndpointRequest, IHttpResult>
{
    private LinkGenerator LinkGenerator { get; }
    private IOpenIdEndpointCollectionProvider OpenIdEndpointCollectionProvider { get; }
    private IHttpResultFactory HttpResultFactory { get; }

    public DiscoveryEndpointHandler(
        LinkGenerator linkGenerator,
        IOpenIdEndpointCollectionProvider openIdEndpointCollectionProvider,
        IHttpResultFactory httpResultFactory)
    {
        LinkGenerator = linkGenerator;
        OpenIdEndpointCollectionProvider = openIdEndpointCollectionProvider;
        HttpResultFactory = httpResultFactory;
    }

    public async ValueTask<IHttpResult> HandleAsync(DiscoveryEndpointRequest request, CancellationToken cancellationToken)
    {
        var routeValues = new { };
        var httpContext = request.HttpContext;
        var discoveryResponse = new Dictionary<string, object>();

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

            discoveryResponse[endpointName] = endpointUrl;
        }

        await ValueTask.CompletedTask;

        return HttpResultFactory.Ok(discoveryResponse);
    }
}
