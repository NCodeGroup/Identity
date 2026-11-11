// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.AspNetCore;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The <see cref="IEndpointProvider"/> that serves the single-page bundle's content-hashed files, such as its scripts
/// and styles, from the bundle's assets directory. A file never changes under its name, so a browser keeps it for a
/// year, and it keeps its validators so a browser that does revalidate it does so cheaply. Nothing outside the assets
/// directory, including the bundle's <c>index.html</c>, is served.
/// </summary>
// See ADR-1254.
internal sealed class InteractiveBundleAssetEndpointHandler(
    SinglePageBundle<InteractiveBundleShell> bundle
) : IEndpointProvider
{
    private const string AssetRouteParameterName = "asset";

    private SinglePageBundle<InteractiveBundleShell> Bundle { get; } = bundle;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        // The user agent loads the files from the page, from the same origin; no CORS policy applies to them.
        var surface = endpoints.MapGroup(string.Empty).WithFrontChannel();

        surface.MapMethods(
            $"{InteractiveAuthenticationDefaults.BasePath}/{SinglePageBundleDefaults.AssetsDirectory}/{{**{AssetRouteParameterName}}}",
            [HttpMethods.Get, HttpMethods.Head],
            GetAsset
        );
    }

    /// <summary>
    /// Serves a file of the single-page bundle's assets directory, such as a hashed script or style, with a
    /// year-long immutable cache lifetime and validators. A path the directory has no file for, or whose type is
    /// unknown, is answered <c>404</c> without a cache lifetime.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The file, or <c>404</c> when there is no such file.</returns>
    public IResult GetAsset(HttpContext httpContext)
    {
        var assetPath =
            httpContext.GetRouteValue(AssetRouteParameterName) as string ?? string.Empty;
        return Bundle.ServeAsset(httpContext.Response, assetPath);
    }
}
