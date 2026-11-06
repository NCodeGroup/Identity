// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// Collects the endpoints that minimal-API calls map, so a data source can build them for one frontend set.
/// </summary>
/// <param name="serviceProvider">The services that the mapped endpoints and their conventions resolve.</param>
internal sealed class BffEndpointCollector(IServiceProvider serviceProvider) : IEndpointRouteBuilder
{
    /// <inheritdoc />
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    public ICollection<EndpointDataSource> DataSources { get; } = new List<EndpointDataSource>();

    /// <inheritdoc />
    public IApplicationBuilder CreateApplicationBuilder() =>
        new ApplicationBuilder(ServiceProvider);
}
