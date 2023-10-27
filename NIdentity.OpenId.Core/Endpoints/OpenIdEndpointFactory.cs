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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Tenants.Commands;

namespace NIdentity.OpenId.Endpoints;

/// <summary>
/// Provides the ability to create <see cref="Endpoint"/> instances for <c>OAuth</c> or <c>OpenID Connect</c> routes.
/// </summary>
public interface IOpenIdEndpointFactory
{
    /// <summary>
    /// Creates a new <see cref="Endpoint"/> instance for the specified <c>OAuth</c> or <c>OpenID Connect</c> handler.
    /// </summary>
    /// <param name="name">The name for the endpoint.</param>
    /// <param name="path">The path for the endpoint.</param>
    /// <param name="httpMethods">The HTTP methods for the endpoint.</param>
    /// <param name="commandFactory">A delegate that is used to create the <see cref="OpenIdEndpointCommand"/> to dispatch requests for the endpoint.</param>
    /// <param name="configureRouteHandlerBuilder">A delegate to configure the <see cref="RouteHandlerBuilder"/> for the endpoint.</param>
    /// <returns>The newly created <see cref="Endpoint"/> for the <c>OAuth</c> or <c>OpenID Connect</c> handler.</returns>
    Endpoint CreateEndpoint(
        string name,
        PathString path,
        IEnumerable<string> httpMethods,
        OpenIdEndpointCommandFactory commandFactory,
        Action<RouteHandlerBuilder>? configureRouteHandlerBuilder = default);
}

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEndpointFactory"/> abstraction.
/// </summary>
public class OpenIdEndpointFactory : IOpenIdEndpointFactory
{
    private OpenIdHostOptions HostOptions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdEndpointFactory"/> class.
    /// </summary>
    public OpenIdEndpointFactory(IOptions<OpenIdHostOptions> hostOptionsAccessor)
    {
        HostOptions = hostOptionsAccessor.Value;
    }

    /// <inheritdoc />
    public Endpoint CreateEndpoint(
        string name,
        PathString path,
        IEnumerable<string> httpMethods,
        OpenIdEndpointCommandFactory commandFactory,
        Action<RouteHandlerBuilder>? configureRouteHandlerBuilder = default)
    {
        var tenantOptions = HostOptions.Tenant;
        var conventions = new List<Action<EndpointBuilder>>();
        var endpointConventionBuilder = new EndpointConventionBuilder(conventions);
        var httpMethodCollection = httpMethods as IReadOnlyCollection<string> ?? httpMethods.ToList();

        var displayName = $"{path} HTTP: {string.Join(", ", httpMethodCollection)}";

        endpointConventionBuilder.WithName(name);
        endpointConventionBuilder.WithGroupName("OpenId"); // TODO
        endpointConventionBuilder.WithDisplayName(displayName);
        endpointConventionBuilder.WithMetadata(new HttpMethodMetadata(httpMethodCollection));

        var routeHandlerBuilder = new RouteHandlerBuilder(new[] { endpointConventionBuilder });
        configureRouteHandlerBuilder?.Invoke(routeHandlerBuilder);

        RoutePattern? tenantRoute = null;
        var tenantPath = tenantOptions.TenantPath;
        if (tenantPath.HasValue)
        {
            tenantRoute = RoutePatternFactory.Parse(tenantPath.Value);
        }

        var descriptor = new DefaultOpenIdEndpointDescriptor(
            name,
            displayName,
            tenantRoute,
            commandFactory);

        var requestDelegate = CreateRequestDelegate(descriptor);

        var relativeRoute = RoutePatternFactory.Parse(path);
        var endpointRoute = RoutePatternFactory.Combine(tenantRoute, relativeRoute);

        const int defaultOrder = 0;
        var routeEndpointBuilder = new RouteEndpointBuilder(requestDelegate, endpointRoute, defaultOrder);

        foreach (var convention in conventions)
        {
            convention(routeEndpointBuilder);
        }

        return routeEndpointBuilder.Build();
    }

    private static RequestDelegate CreateRequestDelegate(OpenIdEndpointDescriptor descriptor) =>
        async httpContext =>
        {
            var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();
            var cancellationToken = httpContext.RequestAborted;
            var propertyBag = descriptor.PropertyBag;

            var openIdTenant = await mediator.SendAsync(
                new GetOpenIdTenantCommand(
                    httpContext,
                    descriptor.TenantRoute,
                    propertyBag.Clone()),
                cancellationToken);

            var openIdContext = new DefaultOpenIdContext(
                httpContext,
                openIdTenant,
                descriptor,
                propertyBag.Clone());

            var openIdCommand = descriptor.CommandFactory(openIdContext);
            var openIdResult = await mediator.SendAsync(openIdCommand, cancellationToken);
            await openIdResult.ExecuteResultAsync(openIdContext, cancellationToken);
        };
}
