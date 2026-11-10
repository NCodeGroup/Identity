// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NCode.AspNetCore;

/// <summary>
/// Removes the icon links of an HTML document's head and creates the link of a replacement icon.
/// </summary>
/// <remarks>
/// A host that serves a single-page bundle on behalf of a tenant uses this class to drop the bundle's own icons and to
/// link the tenant's icon in their place.
/// </remarks>
// See ADR-1494.
[PublicAPI]
public static partial class HtmlIconLinks
{
    /// <summary>
    /// Removes every <c>link</c> element whose <c>rel</c> tokens include <c>icon</c> or <c>apple-touch-icon</c>.
    /// </summary>
    /// <param name="html">The HTML text.</param>
    /// <returns>The HTML text without its icon links; every other character is unchanged.</returns>
    public static string Remove(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return LinkRegex()
            .Replace(html, match => IsIconLink(match.Value) ? string.Empty : match.Value);
    }

    /// <summary>
    /// Creates the <c>link</c> element of an icon.
    /// </summary>
    /// <param name="href">The URL of the icon, which the method encodes for an HTML attribute.</param>
    /// <returns>The <c>link</c> element with <c>rel="icon"</c>.</returns>
    public static string Create(string href)
    {
        ArgumentNullException.ThrowIfNull(href);
        return $"<link rel=\"icon\" href=\"{HtmlEncoder.Default.Encode(href)}\" />";
    }

    private static bool IsIconLink(string tag)
    {
        var match = RelRegex().Match(tag);
        if (!match.Success)
            return false;

        var tokens = match
            .Groups["value"]
            .Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (
                string.Equals(token, "icon", StringComparison.OrdinalIgnoreCase)
                || string.Equals(token, "apple-touch-icon", StringComparison.OrdinalIgnoreCase)
            )
                return true;
        }

        return false;
    }

    [GeneratedRegex(
        "<link\\b(?:[^>\"']|\"[^\"]*\"|'[^']*')*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex LinkRegex();

    [GeneratedRegex(
        "\\srel\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s>\"']+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex RelRegex();
}
