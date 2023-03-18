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

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NIdentity.OpenId.Mediator;

namespace NIdentity.OpenId.Endpoints;

public delegate ValueTask OpenIdRequestDelegate(OpenIdEndpointContext context);

public interface IOpenIdEndpointMiddleware
{
    ValueTask InvokeAsync(OpenIdEndpointContext context, OpenIdRequestDelegate next);
}

public interface IOpenIdEndpointFactory
{
    Endpoint CreateEndpoint(
        string name,
        string path,
        IEnumerable<string> httpMethods,
        Func<OpenIdEndpointContext, OpenIdEndpointRequest> requestFactory,
        Action<RouteHandlerBuilder>? configureRouteHandlerBuilder = default);
}

public class OpenIdEndpointFactory : IOpenIdEndpointFactory
{
    private IMediator Mediator { get; }

    public OpenIdEndpointFactory(IMediator mediator)
    {
        Mediator = mediator;
    }

    public Endpoint CreateEndpoint(
        string name,
        string path,
        IEnumerable<string> httpMethods,
        Func<OpenIdEndpointContext, OpenIdEndpointRequest> requestFactory,
        Action<RouteHandlerBuilder>? configureRouteHandlerBuilder = default)
    {
        var conventions = new List<Action<EndpointBuilder>>();
        var endpointConventionBuilder = new EndpointConventionBuilder(conventions);
        var httpMethodArray = httpMethods as string[] ?? httpMethods.ToArray();

        var displayName = $"{path} HTTP: {string.Join(", ", httpMethodArray)}";

        endpointConventionBuilder.WithName(name);
        endpointConventionBuilder.WithDisplayName(displayName);
        endpointConventionBuilder.WithMetadata(new HttpMethodMetadata(httpMethodArray));

        var routeHandlerBuilder = new RouteHandlerBuilder(new[] { endpointConventionBuilder });
        configureRouteHandlerBuilder?.Invoke(routeHandlerBuilder);

        async Task RequestDelegate(HttpContext httpContext)
        {
            var endpoint = httpContext.GetEndpoint();
            var descriptor = new OpenIdEndpointDescriptor
            {
                Name = name,
                DisplayName = displayName,
                Metadata = endpoint?.Metadata ?? EndpointMetadataCollection.Empty
            };
            var context = new OpenIdEndpointContext(descriptor, httpContext);

            var request = requestFactory(context);
            var result = await Mediator.SendAsync(request, httpContext.RequestAborted);
            await result.ExecuteResultAsync(context);
        }

        const int defaultOrder = 0;
        var routePattern = RoutePatternFactory.Parse(path);
        var routeEndpointBuilder = new RouteEndpointBuilder(RequestDelegate, routePattern, defaultOrder);

        foreach (var convention in conventions)
        {
            convention(routeEndpointBuilder);
        }

        return routeEndpointBuilder.Build();
    }
}
