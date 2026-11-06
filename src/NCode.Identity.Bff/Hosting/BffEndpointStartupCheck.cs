// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.RemoteApis;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Transforms.Builder;

namespace NCode.Identity.Bff.Hosting;

/// <summary>
/// Fails startup when the BFF metadata of an endpoint is incomplete: a YARP route that names a token requirement
/// without <c>AsBffRemoteApis()</c>, a YARP route mapped without <c>AddBffTransforms()</c>, or an endpoint that names
/// a frontend the BFF does not configure, or an endpoint whose <see cref="IBffEndpointStartupRequirement"/> metadata
/// reports a failure.
/// </summary>
/// <remarks>
/// Endpoints are complete only once the pipeline is built, so the check runs at <see cref="StartedAsync"/>. After
/// startup a change of the endpoints or of the frontends is checked again and only logged; the request-time refusals
/// (a <c>502</c> from the token accessor, a <c>404</c> from routing) stay the backstop.
/// </remarks>
// See ADR-1433 and ADR-1442.
internal sealed class BffEndpointStartupCheck(
    EndpointDataSource endpoints,
    BffFrontendCatalog frontends,
    IServiceProvider services,
    IEnumerable<ITransformProvider> transformProviders,
    ILogger<BffEndpointStartupCheck> logger
) : IHostedLifecycleService, IDisposable
{
    /// <summary>
    /// Finds the incomplete BFF metadata of the endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoints to check.</param>
    /// <param name="frontends">The frontends that the BFF serves.</param>
    /// <param name="services">The application's services, which the endpoints' requirements are checked against.</param>
    /// <param name="hasBffTransforms">Whether the BFF's YARP transforms are registered.</param>
    /// <returns>A message for each failure, or an empty list when the metadata is complete.</returns>
    internal static IReadOnlyList<string> FindFailures(
        IEnumerable<Endpoint> endpoints,
        BffFrontendCatalog frontends,
        IServiceProvider services,
        bool hasBffTransforms
    )
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(frontends);
        ArgumentNullException.ThrowIfNull(services);

        var failures = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var frontendAttributes = endpoint.Metadata.GetOrderedMetadata<BffFrontendAttribute>();

            var model = endpoint.Metadata.GetMetadata<RouteModel>();
            if (model is not null)
            {
                var route = model.Config;

                if (!hasBffTransforms)
                {
                    failures.Add(
                        $"The YARP route '{route.RouteId}' is mapped in a BFF host without AddBffTransforms(), which keeps the BFF's cookies from every upstream: call AddReverseProxy().AddBffTransforms()."
                    );
                }

                if (
                    BffReverseProxyRoutes.IsRemoteApi(route)
                    && endpoint.Metadata.GetMetadata<BffApiAttribute>() is null
                )
                {
                    failures.Add(
                        $"The YARP route '{route.RouteId}' names {BffReverseProxyMetadataKeys.TokenRequirement} but its endpoint is not a BFF API: call MapReverseProxy().AsBffRemoteApis()."
                    );
                }

                var name = BffReverseProxyRoutes.GetFrontend(route);
                if (
                    name is not null
                    && !frontendAttributes.Any(attribute =>
                        string.Equals(attribute.Name, name, StringComparison.Ordinal)
                    )
                )
                {
                    failures.Add(
                        $"The YARP route '{route.RouteId}' names {BffReverseProxyMetadataKeys.Frontend} '{name}' but its endpoint is not limited to it: call MapReverseProxy().AsBffRemoteApis()."
                    );
                }
            }

            foreach (var attribute in frontendAttributes)
            {
                if (!frontends.TryGet(attribute.Name, out _))
                {
                    failures.Add(
                        $"The endpoint '{endpoint.DisplayName}' names the BFF frontend '{attribute.Name}', which Bff:Frontends does not configure."
                    );
                }
            }

            var requirements = endpoint
                .Metadata.GetOrderedMetadata<IBffEndpointStartupRequirement>()
                .Distinct();
            foreach (var requirement in requirements)
            {
                var failure = requirement.Validate(services);
                if (failure is not null)
                    failures.Add($"Endpoint '{endpoint.DisplayName}': {failure}");
            }
        }

        return failures;
    }

    private IDisposable? Subscription { get; set; }

    private bool HasBffTransforms => transformProviders.OfType<BffTransformProvider>().Any();

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        var failures = FindFailures(endpoints.Endpoints, frontends, services, HasBffTransforms);
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(" ", failures));

        Subscription = ChangeToken.OnChange(
            () =>
                new CompositeChangeToken([endpoints.GetChangeToken(), frontends.GetChangeToken()]),
            Recheck
        );
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        Subscription?.Dispose();
        Subscription = null;
    }

    private void Recheck()
    {
        foreach (
            var failure in FindFailures(endpoints.Endpoints, frontends, services, HasBffTransforms)
        )
            logger.EndpointCheckFailed(failure);
    }
}
