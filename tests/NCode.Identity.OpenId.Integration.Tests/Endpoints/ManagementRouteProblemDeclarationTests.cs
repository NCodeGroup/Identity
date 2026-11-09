// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
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

    private static string[] AuthenticatedCallerNames { get; } =
    ["api/capabilities/get", "api/me/get", "api/me/teams/list", "api/me/tenants/list"];

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

    private static bool HasAttribute<TMetadata>(ParameterInfo parameter) =>
        parameter.GetCustomAttributes(inherit: true).OfType<TMetadata>().Any();

    private static bool CanFailToBind(ParameterInfo parameter)
    {
        var bindsRequiredValue =
            HasAttribute<IFromQueryMetadata>(parameter)
            || HasAttribute<IFromHeaderMetadata>(parameter);
        if (!bindsRequiredValue && !HasAttribute<IFromRouteMetadata>(parameter))
            return false;

        if (
            (Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType)
            != typeof(string)
        )
            return true;

        return bindsRequiredValue
            && new NullabilityInfoContext().Create(parameter).ReadState == NullabilityState.NotNull
            && !parameter.HasDefaultValue;
    }

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
            Assert.True(
                codes.Contains(StatusCodes.Status409Conflict)
                    && codes.Contains(StatusCodes.Status503ServiceUnavailable),
                endpoint.RoutePattern.RawText
            );
        }
    }

    [Fact]
    public void ManagementRoutes_WhenTheyAuthorizeTheCaller_DeclareUnauthorizedAndForbidden()
    {
        using var factory = new HostApplicationFactory();

        var endpoints = GetManagementEndpoints(factory);

        var endpointNames = endpoints
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .ToHashSet();
        foreach (var name in AuthenticatedCallerNames)
        {
            Assert.Contains(name, endpointNames);
        }

        foreach (var endpoint in endpoints)
        {
            var name = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName;
            if (name is not null && AuthenticatedCallerNames.Contains(name))
            {
                continue;
            }

            var codes = GetProblemStatusCodes(endpoint);
            Assert.True(
                codes.Contains(StatusCodes.Status401Unauthorized)
                    && codes.Contains(StatusCodes.Status403Forbidden),
                endpoint.RoutePattern.RawText
            );
        }
    }

    [Fact]
    public void ManagementRoutes_WhenTheyOnlyAuthenticateTheCaller_DeclareUnauthorizedButNotForbidden()
    {
        using var factory = new HostApplicationFactory();

        var endpoints = GetManagementEndpoints(factory);

        foreach (var name in AuthenticatedCallerNames)
        {
            var endpoint = Assert.Single(
                endpoints,
                e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name
            );

            var codes = GetProblemStatusCodes(endpoint);
            Assert.Contains(StatusCodes.Status401Unauthorized, codes);
            Assert.DoesNotContain(StatusCodes.Status403Forbidden, codes);
        }
    }

    [Fact]
    public void ManagementRoutes_WhenTheyBindABody_DeclareBadRequestAndUnsupportedMediaType()
    {
        using var factory = new HostApplicationFactory();

        var matched = 0;
        foreach (var endpoint in GetManagementEndpoints(factory))
        {
            var method = endpoint.Metadata.OfType<MethodInfo>().FirstOrDefault();
            if (method is null || !method.GetParameters().Any(HasAttribute<IFromBodyMetadata>))
            {
                continue;
            }

            matched++;
            var codes = GetProblemStatusCodes(endpoint);
            Assert.True(
                codes.Contains(StatusCodes.Status400BadRequest)
                    && codes.Contains(StatusCodes.Status415UnsupportedMediaType),
                endpoint.RoutePattern.RawText
            );
        }

        Assert.NotEqual(0, matched);
    }

    [Fact]
    public void ManagementRoutes_WhenAParameterCanFailToBind_DeclareBadRequest()
    {
        using var factory = new HostApplicationFactory();

        var matched = 0;
        foreach (var endpoint in GetManagementEndpoints(factory))
        {
            var method = endpoint.Metadata.OfType<MethodInfo>().FirstOrDefault();
            if (method is null || !method.GetParameters().Any(CanFailToBind))
            {
                continue;
            }

            matched++;
            Assert.Contains(StatusCodes.Status400BadRequest, GetProblemStatusCodes(endpoint));
        }

        Assert.NotEqual(0, matched);
    }

    #endregion
}
