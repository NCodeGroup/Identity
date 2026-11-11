// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.AspNetCore;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// The console bundle's <c>index.html</c>, which each response renders with its own nonce.
/// </summary>
/// <param name="Html">The text of the bundle's <c>index.html</c>.</param>
internal sealed record ManagementConsolePage(string Html)
{
    private string HtmlWithoutIcons { get; } = HtmlIconLinks.Remove(Html);

    /// <summary>
    /// Replaces every nonce placeholder of the page with a nonce and, when a tab icon is configured,
    /// links it in place of the bundle's own icons.
    /// </summary>
    /// <param name="nonce">The nonce of the response's Content-Security-Policy.</param>
    /// <param name="faviconUrl">
    /// The absolute <c>https</c> URL of the icon the browser shows in the console's tab, written as given, or
    /// <see langword="null"/> to keep the bundle's own icons.
    /// </param>
    /// <returns>The page that the response writes.</returns>
    public string Render(string nonce, string? faviconUrl)
    {
        var html = faviconUrl is null ? Html : WithIcon(faviconUrl);
        return html.Replace(
            ManagementConsoleDefaults.NoncePlaceholder,
            nonce,
            StringComparison.Ordinal
        );
    }

    // A page without a head has nowhere to link the icon, so it keeps the bundle's own icons.
    private string WithIcon(string faviconUrl)
    {
        var index = HtmlWithoutIcons.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return Html;
        }

        var iconLink = HtmlIconLinks.Create(faviconUrl);
        return HtmlWithoutIcons.Insert(index, iconLink);
    }
}
