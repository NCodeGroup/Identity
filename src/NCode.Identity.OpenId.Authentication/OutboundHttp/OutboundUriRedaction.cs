// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Gives the form of a fetched URL that a log entry carries: scheme, host, port and path, without the user
/// information, query or fragment, which can carry a password, a token or a signed parameter.
/// </summary>
internal static class OutboundUriRedaction
{
    /// <summary>
    /// Gives the form of a URI that a log entry carries.
    /// </summary>
    /// <param name="uri">The URI that a guarded fetch targets.</param>
    /// <returns>The URI without its user information, query or fragment.</returns>
    public static string ForLog(Uri uri) =>
        uri.IsAbsoluteUri ? WithoutUserInfoQueryOrFragment(uri) : StripQuery(uri.OriginalString);

    /// <summary>
    /// Gives the form of a URI string that a log entry carries.
    /// </summary>
    /// <param name="uri">The URI text that a guarded fetch targets.</param>
    /// <returns>The URI text without its user information, query or fragment.</returns>
    public static string ForLog(string uri) =>
        AbsoluteUri.TryParse(uri, out var parsed)
            ? WithoutUserInfoQueryOrFragment(parsed)
            : StripQuery(uri);

    private static string WithoutUserInfoQueryOrFragment(Uri uri) =>
        uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);

    private static string StripQuery(string value)
    {
        var index = value.IndexOfAny(['?', '#']);
        return index < 0 ? value : value[..index];
    }
}
