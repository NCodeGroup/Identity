// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// The checks that a remote API's options must pass, shared by the options validator and by
/// <c>MapBffRemoteApi</c>.
/// </summary>
internal static class BffRemoteApiValidation
{
    /// <summary>
    /// Validates one remote API's options.
    /// </summary>
    /// <param name="key">The configuration key that failure messages name, such as <c>Bff:RemoteApis:orders</c>.</param>
    /// <param name="api">The options to validate.</param>
    /// <param name="isDevelopment">Whether the host runs in Development, which allows an <c>http</c> destination.</param>
    /// <param name="failures">The list that each failure is added to.</param>
    public static void Validate(
        string key,
        BffRemoteApiOptions? api,
        bool isDevelopment,
        List<string> failures
    )
    {
        if (api is null)
        {
            failures.Add($"{key} must be set.");
            return;
        }

        if (!IsValidPath(api.Path))
        {
            failures.Add(
                $"{key}:Path '{api.Path}' must be a local path that starts with '/', does not end with '/', has no empty, '.' or '..' segment, and uses only letters, digits, '-', '.', '_' and '~'."
            );
        }

        if (!IsValidDestination(api.Destination, isDevelopment))
        {
            failures.Add(
                $"{key}:Destination {QuoteUrl(api.Destination)} must be an absolute URL without user info, a query or a fragment, and must use 'https' outside Development."
            );
        }

        if (string.IsNullOrWhiteSpace(api.TokenRequirement))
        {
            failures.Add($"{key}:TokenRequirement must not be blank.");
        }

        if (api.AllowedMethods is null)
        {
            failures.Add($"{key}:AllowedMethods must be set.");
        }
        else if (api.AllowedMethods.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{key}:AllowedMethods must not contain a blank entry.");
        }

        if (api.Timeout <= TimeSpan.Zero)
        {
            failures.Add($"{key}:Timeout must be greater than zero.");
        }
    }

    /// <summary>
    /// Gets whether two local path prefixes overlap: they are equal, or one is a parent segment path of the other.
    /// </summary>
    /// <param name="first">The first path prefix.</param>
    /// <param name="second">The second path prefix.</param>
    /// <returns><c>true</c> when a request path could match both prefixes.</returns>
    public static bool Overlaps(string first, string second) =>
        IsSameOrParent(first, second) || IsSameOrParent(second, first);

    private static bool IsSameOrParent(string parent, string child) =>
        child.StartsWith(parent, StringComparison.OrdinalIgnoreCase)
        && (child.Length == parent.Length || child[parent.Length] == '/');

    private static bool IsValidPath(string? path)
    {
        if (
            string.IsNullOrEmpty(path)
            || path.Length < 2
            || path[0] != '/'
            || path[^1] == '/'
            || path.Contains("//", StringComparison.Ordinal)
            || !BffReturnUrls.IsWellFormedPath(path)
        )
        {
            return false;
        }

        return path.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '.' or '_' or '~');
    }

    private static bool IsValidDestination(string? destination, bool isDevelopment) =>
        AbsoluteUri.TryParse(destination, out var uri)
        && uri.UserInfo.Length == 0
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0
        && !destination.Contains('?', StringComparison.Ordinal)
        && !destination.Contains('#', StringComparison.Ordinal)
        && (
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || (
                isDevelopment
                && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            )
        );

    // A URL with user info may carry a password, which a failure message never quotes.
    private static string QuoteUrl(string? url) =>
        url is not null && url.Contains('@', StringComparison.Ordinal)
            ? "(withheld because it contains '@')"
            : $"'{url}'";
}
