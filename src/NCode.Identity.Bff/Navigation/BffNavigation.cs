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

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Navigation;

/// <summary>
/// Builds the absolute and local URLs that the BFF endpoints redirect to.
/// </summary>
internal static class BffNavigation
{
    /// <summary>
    /// Gets the absolute redirect URI of the callback endpoint.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="options">The BFF options.</param>
    public static string CallbackUri(HttpRequest request, BffOptions options) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}{options.PathPrefix}/callback";

    /// <summary>
    /// Gets a local URL for a path, prefixed with the path base.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="path">The local path.</param>
    public static string LocalUrl(HttpRequest request, string path) => $"{request.PathBase}{path}";

    /// <summary>
    /// Gets the local URL of the error page with the reason in the <c>error</c> query parameter.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="options">The BFF options.</param>
    /// <param name="reason">The error reason.</param>
    public static string ErrorUrl(HttpRequest request, BffOptions options, string reason)
    {
        var errorPath = LocalUrl(request, options.ErrorPath);
        var escaped = Uri.EscapeDataString(reason);
        return $"{errorPath}?error={escaped}";
    }

    /// <summary>
    /// Gets an absolute URL for a path.
    /// </summary>
    /// <param name="request">The current request.</param>
    /// <param name="path">The local path.</param>
    public static string AbsoluteUrl(HttpRequest request, string path) =>
        $"{request.Scheme}://{request.Host}{request.PathBase}{path}";
}
