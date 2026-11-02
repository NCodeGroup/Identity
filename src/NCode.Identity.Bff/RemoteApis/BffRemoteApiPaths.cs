// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Extracts the part of a request path that a remote API forwards, and refuses any path that could leave the
/// destination's base path once the upstream server decodes it.
/// </summary>
internal static class BffRemoteApiPaths
{
    /// <summary>
    /// The name of the catch-all route parameter that holds the part of the path after a remote API's prefix.
    /// </summary>
    public const string RemainderParameter = "remainder";

    /// <summary>
    /// Gets the part of the request path after the remote API's prefix, when it is safe to forward. The prefix is the
    /// whole route the endpoint matched before its catch-all parameter, a route group's prefix included.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <param name="remainder">The part of the path after the prefix: empty, or starting with <c>/</c>.</param>
    /// <returns>
    /// <c>true</c> when the request path ends with the matched remainder and has no dot segment, backslash or encoded
    /// slash, either decoded or as it was sent.
    /// </returns>
    public static bool TryGetRemainder(HttpContext context, out PathString remainder)
    {
        remainder = PathString.Empty;

        var path = context.Request.Path.Value ?? string.Empty;
        var value = context.Request.RouteValues[RemainderParameter] as string;
        int start;
        if (string.IsNullOrEmpty(value))
        {
            start = path.EndsWith('/') ? path.Length - 1 : path.Length;
        }
        else
        {
            start = path.Length - value.Length - 1;
            if (start < 0 || path[start] != '/' || !path.EndsWith(value, StringComparison.Ordinal))
                return false;
        }

        if (!IsSafeDecodedPath(path))
            return false;

        var rawTarget = context.Features.Get<IHttpRequestFeature>()?.RawTarget;
        if (!string.IsNullOrEmpty(rawTarget) && rawTarget[0] == '/' && !IsSafeRawPath(rawTarget))
            return false;

        remainder = new PathString(path[start..]);
        return true;
    }

    private static bool IsSafeDecodedPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        if (
            path.Contains('\\', StringComparison.Ordinal)
            || path.Contains("%2F", StringComparison.OrdinalIgnoreCase)
            || path.Contains("%5C", StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        return !HasDotSegment(path.Split('/'));
    }

    private static bool IsSafeRawPath(string rawTarget)
    {
        var queryStart = rawTarget.IndexOf('?', StringComparison.Ordinal);
        var rawPath = queryStart < 0 ? rawTarget : rawTarget[..queryStart];

        var segments = rawPath.Split('/').Select(Uri.UnescapeDataString).ToArray();
        if (segments.Any(segment => segment.Contains('/') || segment.Contains('\\')))
            return false;

        return !HasDotSegment(segments);
    }

    private static bool HasDotSegment(IEnumerable<string> segments) =>
        segments.Any(segment => segment is "." or "..");
}
