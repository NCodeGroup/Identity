// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Serves the console bundle's content-hashed files, such as its scripts and styles, from the bundle's assets
/// directory. A file never changes under its name, so a browser keeps it for a year, and it keeps its validators so a
/// browser that does revalidate it does so cheaply. Nothing outside the assets directory, including the bundle's
/// <c>index.html</c>, is served.
/// </summary>
internal sealed class ManagementConsoleAssetHandler(ManagementConsoleBundleReader reader)
{
    /// <summary>
    /// The name of the route value that holds the asset's path relative to the assets directory.
    /// </summary>
    public const string AssetRouteParameterName = "asset";

    // A file's name carries its content hash, so no copy of it can go stale: a browser keeps it for a year.
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    private static FileExtensionContentTypeProvider AssetContentTypes { get; } = new();

    /// <summary>
    /// Serves a file of the bundle's assets directory with a year-long immutable cache lifetime and validators. A path
    /// the directory has no file for, or whose type is unknown, is answered <c>404</c> without a cache lifetime.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>The file, or <c>404</c> when there is no such file.</returns>
    public IResult Handle(HttpContext httpContext)
    {
        var assetPath =
            httpContext.GetRouteValue(AssetRouteParameterName) as string ?? string.Empty;
        var file = reader.TryGetAsset(assetPath);
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
