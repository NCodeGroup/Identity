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
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NIdentity.OpenId.Mediator;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Provides the ability to create <see cref="Endpoint"/> instances for <c>OAuth</c> or <c>OpenID Connect</c> routes.
/// </summary>
public interface IOpenIdEndpointFactory
{
    /// <summary>
    /// Creates a new <see cref="Endpoint"/> instance for the specified <c>OAuth</c> or <c>OpenID Connect</c> route.
    /// </summary>
    /// <param name="name">The name for the endpoint.</param>
    /// <param name="path">The route pattern for the endpoint.</param>
    /// <param name="httpMethods">The HTTP methods for the endpoint.</param>
    /// <param name="commandFactory">A delegate that is used to create the <see cref="OpenIdEndpointCommand"/> to dispatch requests for the endpoint.</param>
    /// <param name="configureRouteHandlerBuilder">A delegate to configure the <see cref="RouteHandlerBuilder"/> for the endpoint.</param>
    /// <returns>The newly created <see cref="Endpoint"/> for the <c>OAuth</c> or <c>OpenID Connect</c> route.</returns>
    Endpoint CreateEndpoint(
        string name,
        string path,
        IEnumerable<string> httpMethods,
        Func<OpenIdContext, OpenIdEndpointCommand> commandFactory,
        Action<RouteHandlerBuilder>? configureRouteHandlerBuilder = default);
}

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEndpointFactory"/> abstraction.
/// </summary>
public class OpenIdEndpointFactory : IOpenIdEndpointFactory
{
    private IMediator Mediator { get; }
    private IOpenIdContextFactory OpenIdContextFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdEndpointFactory"/> class.
    /// </summary>
    public OpenIdEndpointFactory(IMediator mediator, IOpenIdContextFactory openIdContextFactory)
    {
        Mediator = mediator;
        OpenIdContextFactory = openIdContextFactory;
    }

    /// <inheritdoc />
    public Endpoint CreateEndpoint(
        string name,
        string path,
        IEnumerable<string> httpMethods,
        Func<OpenIdContext, OpenIdEndpointCommand> commandFactory,
        Action<RouteHandlerBuilder>? configureRouteHandlerBuilder = default)
    {
        var conventions = new List<Action<EndpointBuilder>>();
        var endpointConventionBuilder = new EndpointConventionBuilder(conventions);
        var httpMethodCollection = httpMethods as IReadOnlyCollection<string> ?? httpMethods.ToList();

        var displayName = $"{path} HTTP: {string.Join(", ", httpMethodCollection)}";

        var descriptor = new OpenIdEndpointDescriptor
        {
            Name = name,
            DisplayName = displayName
        };

        endpointConventionBuilder.WithName(name);
        endpointConventionBuilder.WithGroupName("OpenId"); // TODO
        endpointConventionBuilder.WithDisplayName(displayName);
        endpointConventionBuilder.WithMetadata(new HttpMethodMetadata(httpMethodCollection));

        var routeHandlerBuilder = new RouteHandlerBuilder(new[] { endpointConventionBuilder });
        configureRouteHandlerBuilder?.Invoke(routeHandlerBuilder);

        async Task RequestDelegate(HttpContext httpContext)
        {
            var cancellationToken = httpContext.RequestAborted;
            var openIdContext = await OpenIdContextFactory.CreateAsync(httpContext, descriptor, cancellationToken);
            var openIdCommand = commandFactory(openIdContext);
            var openIdResult = await Mediator.SendAsync(openIdCommand, cancellationToken);
            await openIdResult.ExecuteResultAsync(openIdContext, cancellationToken);
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
