// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using NCode.Identity.AspNetCore;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// The route group of the setup surface (<c>/setup</c>): the setup page and its single-page routes, the page's
/// content-hashed assets, and the status and root-creation API the page calls. The user agent navigates to the page
/// and the page calls the API from the same origin, so no CORS policy applies. The routes have confidential bodies, so
/// HTTP logging never writes a request or response body, and the group is excluded from the OpenAPI description. A
/// literal route wins over the page's catch-all, so the surface is reachable whatever the tenant route strategy.
/// </summary>
internal sealed class SetupEndpointGroup : IEndpointGroup
{
    /// <summary>
    /// The <see cref="IEndpointGroup.Name"/> of the setup group.
    /// </summary>
    public const string GroupName = "setup";

    /// <inheritdoc />
    public string Name => GroupName;

    /// <inheritdoc />
    public RoutePattern GetPrefix(IServiceProvider serviceProvider) =>
        RoutePatternFactory.Parse(SetupDefaults.PathPrefix);

    /// <inheritdoc />
    public void ConfigureRoutes(RouteGroupBuilder group)
    {
        group.WithFrontChannel().WithConfidentialBodies().ExcludeFromDescription();

        group.MapGet("/", HandlePageAsync);
        group.MapGet("/{**rest}", HandlePageAsync);

        group.MapMethods(
            $"/assets/{{**{SetupPageHandler.AssetRouteParameterName}}}",
            [HttpMethods.Get, HttpMethods.Head],
            static ([FromServices] SetupPageHandler handler, HttpContext httpContext) =>
                handler.HandleAssetAsync(httpContext, httpContext.RequestAborted)
        );

        group.MapGet(
            "/api/status",
            static ([FromServices] SetupApiHandler handler, HttpContext httpContext) =>
                handler.GetStatusAsync(httpContext, httpContext.RequestAborted)
        );

        // The body is not a parameter: the handler reads it after it checks that the surface is served.
        group.MapPost(
            "/api/root",
            static ([FromServices] SetupApiHandler handler, HttpContext httpContext) =>
                handler.CreateRootAsync(httpContext, httpContext.RequestAborted)
        );
    }

    private static ValueTask<IResult> HandlePageAsync(
        [FromServices] SetupPageHandler handler,
        HttpContext httpContext
    ) => handler.HandlePageAsync(httpContext, httpContext.RequestAborted);
}
