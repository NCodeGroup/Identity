// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Navigation;

/// <summary>
/// Builds the absolute and local URLs that the BFF endpoints redirect to. An absolute URL comes from
/// <see cref="BffFrontendOptions.PublicOrigin"/>, never from the request; a local URL takes the request's path base.
/// </summary>
// See ADR-1365.
internal static class BffNavigation
{
    /// <summary>
    /// Gets the absolute redirect URI of the callback endpoint of a frontend.
    /// </summary>
    /// <param name="options">The options of the frontend.</param>
    public static string CallbackUri(BffFrontendOptions options) =>
        $"{options.PublicOrigin}{options.PathPrefix}/callback";

    /// <summary>
    /// Gets a local URL for a path, prefixed with the path base.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="path">The local path.</param>
    public static string LocalUrl(HttpRequest request, string path) => $"{request.PathBase}{path}";

    /// <summary>
    /// Gets the local URL of the error page of a frontend with the reason in the <c>error</c> query parameter.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="options">The options of the frontend.</param>
    /// <param name="reason">The error reason.</param>
    public static string ErrorUrl(HttpRequest request, BffFrontendOptions options, string reason)
    {
        var errorPath = LocalUrl(request, options.ErrorPath);
        var escaped = Uri.EscapeDataString(reason);
        return $"{errorPath}?error={escaped}";
    }

    /// <summary>
    /// Gets an absolute URL for a path under the public origin of a frontend.
    /// </summary>
    /// <param name="options">The options of the frontend.</param>
    /// <param name="path">The local path.</param>
    public static string AbsoluteUrl(BffFrontendOptions options, string path) =>
        $"{options.PublicOrigin}{path}";
}
