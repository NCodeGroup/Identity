// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Gives the form of a fetched URL that a log entry carries: scheme, authority and path, without the query or
/// fragment, which can carry a token or a signed parameter.
/// </summary>
internal static class OutboundUriRedaction
{
    /// <summary>
    /// Gives the form of a URI that a log entry carries.
    /// </summary>
    /// <param name="uri">The URI that a guarded fetch targets.</param>
    /// <returns>The URI without its query or fragment.</returns>
    public static string ForLog(Uri uri) =>
        uri.IsAbsoluteUri ? uri.GetLeftPart(UriPartial.Path) : StripQuery(uri.OriginalString);

    /// <summary>
    /// Gives the form of a URI string that a log entry carries.
    /// </summary>
    /// <param name="uri">The URI text that a guarded fetch targets.</param>
    /// <returns>The URI text without its query or fragment.</returns>
    public static string ForLog(string uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            ? parsed.GetLeftPart(UriPartial.Path)
            : StripQuery(uri);

    private static string StripQuery(string value)
    {
        var index = value.IndexOfAny(['?', '#']);
        return index < 0 ? value : value[..index];
    }
}
