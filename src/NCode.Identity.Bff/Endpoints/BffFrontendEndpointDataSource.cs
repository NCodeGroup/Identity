// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Bff.Frontends;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Maps each frontend of the current set and maps them again when the set changes; add it to a route group so the
/// group's conventions apply to every frontend.
/// </summary>
/// <param name="catalog">Provides the current set of frontends and signals when it changes.</param>
/// <param name="services">The services that the mapped endpoints and their conventions resolve.</param>
/// <param name="mapFrontend">Maps the endpoints of one frontend.</param>
// See ADR-1442.
internal sealed class BffFrontendEndpointDataSource(
    BffFrontendCatalog catalog,
    IServiceProvider services,
    Action<IEndpointRouteBuilder, BffFrontend> mapFrontend
) : EndpointDataSource
{
    private Lock Gate { get; } = new();

    private (BffFrontendSet Set, IReadOnlyList<Endpoint> Endpoints)? Cache { get; set; }

    /// <inheritdoc />
    public override IReadOnlyList<Endpoint> Endpoints
    {
        get
        {
            var set = catalog.Current;

            lock (Gate)
            {
                if (Cache is { } cache && ReferenceEquals(cache.Set, set))
                    return cache.Endpoints;

                var collector = new BffEndpointCollector(services);
                foreach (var frontend in set.Frontends)
                    mapFrontend(collector, frontend);

                IReadOnlyList<Endpoint> endpoints =
                [
                    .. collector.DataSources.SelectMany(x => x.Endpoints),
                ];

                Cache = (set, endpoints);
                return endpoints;
            }
        }
    }

    /// <inheritdoc />
    public override IChangeToken GetChangeToken() => catalog.GetChangeToken();
}
