// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Reads the problem statuses an endpoint declares in its OpenAPI metadata.
/// </summary>
[ExcludeFromCodeCoverage]
public static class EndpointProblemMetadata
{
    /// <summary>
    /// Maps <paramref name="provider"/> and returns the problem statuses, in ascending order, that the route with the
    /// given HTTP method and pattern declares through <c>ProducesProblem</c> or <c>ProducesValidationProblem</c>. It
    /// counts validation problems too.
    /// </summary>
    public static async Task<IReadOnlyList<int>> ReadStatusCodesAsync(
        IEndpointProvider provider,
        string httpMethod,
        string routePattern
    )
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(httpMethod);
        ArgumentNullException.ThrowIfNull(routePattern);

        await using var app = WebApplication.CreateSlimBuilder().Build();
        provider.Map(app);

        var endpoint = ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(x =>
                string.Equals(
                    x.RoutePattern.RawText?.TrimEnd('/'),
                    routePattern.TrimEnd('/'),
                    StringComparison.Ordinal
                )
                && x.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(httpMethod)
                    == true
            );

        return endpoint
            .Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(x => x.Type is not null && typeof(ProblemDetails).IsAssignableFrom(x.Type))
            .Select(x => x.StatusCode)
            .Order()
            .ToList();
    }
}
