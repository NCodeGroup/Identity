// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.AspNetCore;

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
    /// <param name="faviconUri">
    /// The absolute <c>https</c> URI of the icon the browser shows in the console's tab, or <see langword="null"/>
    /// to keep the bundle's own icons.
    /// </param>
    /// <returns>The page that the response writes.</returns>
    public string Render(string nonce, Uri? faviconUri)
    {
        var html = faviconUri is null ? Html : WithIcon(faviconUri);
        return html.Replace(
            ManagementConsoleDefaults.NoncePlaceholder,
            nonce,
            StringComparison.Ordinal
        );
    }

    private string WithIcon(Uri faviconUri)
    {
        var index = HtmlWithoutIcons.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        return index < 0
            ? HtmlWithoutIcons
            : HtmlWithoutIcons.Insert(index, HtmlIconLinks.Create(faviconUri.AbsoluteUri));
    }
}
