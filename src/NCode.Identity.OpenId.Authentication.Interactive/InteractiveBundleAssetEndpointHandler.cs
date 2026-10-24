#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The <see cref="IEndpointProvider"/> that serves the single-page bundle's content-hashed files, such as its scripts
/// and styles, from the bundle's assets directory. A file never changes under its name, so a browser keeps it for a
/// year, and it keeps its validators so a browser that does revalidate it does so cheaply. Nothing outside the assets
/// directory, including the bundle's <c>index.html</c>, is served.
/// </summary>
// See ADR-0254.
internal sealed class InteractiveBundleAssetEndpointHandler(InteractiveBundleReader bundleReader)
    : IEndpointProvider
{
    private const string AssetRouteParameterName = "asset";

    // A file's name carries its content hash, so no copy of it can go stale: a browser keeps it for a year.
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    private static FileExtensionContentTypeProvider AssetContentTypes { get; } = new();

    private InteractiveBundleReader BundleReader { get; } = bundleReader;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        // The user agent loads the files from the page, from the same origin; no CORS policy applies to them.
        var surface = endpoints.MapGroup(string.Empty).WithOpenIdFrontChannel();

        surface.MapMethods(
            $"{InteractiveAuthenticationDefaults.BasePath}/{InteractiveBundleReader.AssetsDirectory}/{{**{AssetRouteParameterName}}}",
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
        var file = BundleReader.TryGetAsset(assetPath);
        if (
            file is not { PhysicalPath: { } physicalPath }
            || !AssetContentTypes.TryGetContentType(file.Name, out var contentType)
        )
        {
            return TypedResults.NotFound();
        }

        httpContext.Response.Headers.CacheControl = ImmutableCacheControl;

        var lastModified = file.LastModified;
        var entityTag = new EntityTagHeaderValue(
            $"\"{(lastModified.ToFileTime() ^ file.Length).ToString("x", CultureInfo.InvariantCulture)}\""
        );
        return TypedResults.PhysicalFile(
            physicalPath,
            contentType,
            lastModified: lastModified,
            entityTag: entityTag,
            enableRangeProcessing: true
        );
    }
}
