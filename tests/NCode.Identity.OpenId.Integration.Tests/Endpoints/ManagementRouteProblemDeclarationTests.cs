// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Walks every management route the host maps and asserts the problems each declares.
/// </summary>
public class ManagementRouteProblemDeclarationTests
{
    #region Helpers

    private static List<RouteEndpoint> GetManagementEndpoints(HostApplicationFactory factory) =>
        factory
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(e =>
                e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true
            )
            .ToList();

    private static HashSet<int> GetProblemStatusCodes(Endpoint endpoint) =>
        endpoint
            .Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(metadata =>
                metadata.Type is not null && typeof(ProblemDetails).IsAssignableFrom(metadata.Type)
            )
            .Select(metadata => metadata.StatusCode)
            .ToHashSet();

    #endregion

    #region Route Tests

    [Fact]
    public void ManagementRoutes_Always_DeclareTheRouteGroupProblems()
    {
        using var factory = new HostApplicationFactory();

        var endpoints = GetManagementEndpoints(factory);

        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var codes = GetProblemStatusCodes(endpoint);
            Assert.True(codes.Contains(409) && codes.Contains(503), endpoint.RoutePattern.RawText);
        }
    }

    [Fact]
    public void ManagementRoutes_WhenTheyAuthorizeTheCaller_DeclareUnauthorizedAndForbidden()
    {
        string[] unauthorizedNames =
        [
            "api/me/get",
            "api/me/teams/list",
            "api/me/tenants/list",
            "api/capabilities/get",
        ];

        using var factory = new HostApplicationFactory();

        var endpoints = GetManagementEndpoints(factory);

        var endpointNames = endpoints
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .ToHashSet();
        foreach (var name in unauthorizedNames)
        {
            Assert.Contains(name, endpointNames);
        }

        foreach (var endpoint in endpoints)
        {
            var name = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
            if (name is not null && unauthorizedNames.Contains(name))
            {
                continue;
            }

            var codes = GetProblemStatusCodes(endpoint);
            Assert.True(codes.Contains(401) && codes.Contains(403), endpoint.RoutePattern.RawText);
        }
    }

    #endregion
}
