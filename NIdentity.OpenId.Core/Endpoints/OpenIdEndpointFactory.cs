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
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Options;
using NIdentity.OpenId.Stores;
using NIdentity.OpenId.Tenants;

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
    private OpenIdHostOptions HostOptions { get; }
    private IMediator Mediator { get; }
    private IOpenIdContextFactory OpenIdContextFactory { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdEndpointFactory"/> class.
    /// </summary>
    public OpenIdEndpointFactory(
        IOptions<OpenIdHostOptions> hostOptionsAccessor,
        IMediator mediator,
        IOpenIdContextFactory openIdContextFactory)
    {
        HostOptions = hostOptionsAccessor.Value;
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

        var descriptor = new DefaultOpenIdEndpointDescriptor(name, displayName);

        endpointConventionBuilder.WithName(name);
        endpointConventionBuilder.WithGroupName("OpenId"); // TODO
        endpointConventionBuilder.WithDisplayName(displayName);
        endpointConventionBuilder.WithMetadata(new HttpMethodMetadata(httpMethodCollection));

        var routeHandlerBuilder = new RouteHandlerBuilder(new[] { endpointConventionBuilder });
        configureRouteHandlerBuilder?.Invoke(routeHandlerBuilder);

        var tenantSelector = GetTenantSelector();

        async Task RequestDelegate(HttpContext httpContext)
        {
            var cancellationToken = httpContext.RequestAborted;
            var openIdContext = await OpenIdContextFactory.CreateAsync(httpContext, descriptor, cancellationToken);
            var openIdCommand = commandFactory(openIdContext);
            var openIdResult = await Mediator.SendAsync(openIdCommand, cancellationToken);
            await openIdResult.ExecuteResultAsync(openIdContext, cancellationToken);
        }

        var relativePathRoute = RoutePatternFactory.Parse(path);
        var routePattern = RoutePatternFactory.Combine(
            tenantSelector.BaseRoute,
            relativePathRoute);

        const int defaultOrder = 0;
        var routeEndpointBuilder = new RouteEndpointBuilder(RequestDelegate, routePattern, defaultOrder);

        foreach (var convention in conventions)
        {
            convention(routeEndpointBuilder);
        }

        return routeEndpointBuilder.Build();
    }

    private TenantSelector GetTenantSelector()
    {
        switch (HostOptions.Tenant.Mode)
        {
            case TenantMode.Static:
                return GetStaticSingleTenantRoute();

            case TenantMode.DynamicByPath:
                return GetDynamicByPathTenantRoute();

            case TenantMode.DynamicByHost:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }


    private static Exception MissingTenancyConfigurationException(TenantMode mode) =>
        new InvalidOperationException($"Tenancy Mode is {mode} but the corresponding configuration is missing.");

    private StaticTenantSelector GetStaticSingleTenantRoute()
    {
        var tenantOptions = HostOptions.Tenant.StaticSingle ??
                            throw MissingTenancyConfigurationException(TenantMode.Static);

        RoutePattern? baseRoute = null;
        var basePath = tenantOptions.BasePath;
        if (basePath is { HasValue: true, Value: not ['/'] })
            baseRoute = RoutePatternFactory.Parse(basePath);

        return new StaticTenantSelector(baseRoute, tenantOptions);
    }

    private DynamicByPathTenantSelector GetDynamicByPathTenantRoute()
    {
        var tenantOptions = HostOptions.Tenant.DynamicByPath ??
                            throw MissingTenancyConfigurationException(TenantMode.DynamicByPath);

        var tenantIdRouteParameterName = tenantOptions.TenantIdRouteParameterName;

        var basePath = tenantOptions.BasePath;
        if (!basePath.HasValue)
            basePath = $"/{tenantIdRouteParameterName}";

        var baseRoute = RoutePatternFactory.Parse(basePath);

        // TODO: better exception/message

        if (baseRoute.Parameters.Count == 0)
            throw new InvalidOperationException();

        if (baseRoute.Parameters.Count > 1)
            throw new InvalidOperationException();

        if (baseRoute.Parameters[0].Name != tenantIdRouteParameterName)
            throw new InvalidOperationException();

        return new DynamicByPathTenantSelector(
            baseRoute,
            tenantIdRouteParameterName);
    }

    private void GetDynamicByHostTenantRoute(ICollection<RoutePattern> routePatterns)
    {
        var options = HostOptions.Tenant.DynamicByHost ??
                      throw new InvalidOperationException("Tenancy Mode is DynamicByHost but the corresponding configuration is missing.");

        var basePath = options.BasePath;
        if (string.IsNullOrEmpty(basePath) || basePath is ['/'])
            return;

        var routePattern = RoutePatternFactory.Parse(basePath);
        routePatterns.Add(routePattern);
    }

    private RoutePattern GetBaseRoutePattern()
    {
        var effectivePath = PathString.Empty;

        switch (HostOptions.Tenant.Mode)
        {
            case TenantMode.Static:
                //effectivePath.Add(HostOptions.Tenancy.StaticSingle)
                break;

            case TenantMode.DynamicByPath:
                if (HostOptions.Tenant.DynamicByPath == null)
                    // TODO: better exception/message
                    throw new InvalidOperationException();
                effectivePath.Add(HostOptions.Tenant.DynamicByPath.RoutePattern);
                var tenantPattern = RoutePatternFactory.Parse(HostOptions.Tenant.DynamicByPath.RoutePattern);
                if (tenantPattern.Parameters.All(parameter => parameter.Name != HostOptions.Tenant.DynamicByPath.TenantIdRouteParameterName))
                    // TODO: better exception/message
                    throw new InvalidOperationException();
                break;

            case TenantMode.DynamicByHost:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        var endpointBasePath = HostOptions.EndpointBasePath;
        if (!string.IsNullOrEmpty(endpointBasePath))
        {
            if (endpointBasePath[0] != '/')
                endpointBasePath = '/' + endpointBasePath;

            effectivePath.Add(endpointBasePath);
        }

        RoutePatternFactory.Parse()
    }
}
