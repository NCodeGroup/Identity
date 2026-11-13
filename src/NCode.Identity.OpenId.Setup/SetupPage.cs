// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// The setup bundle's <c>index.html</c>, which each response renders with its own nonce and with a base URL that
/// resolves the bundle's relative asset URLs under the setup path.
/// </summary>
/// <param name="Html">The text of the bundle's <c>index.html</c>.</param>
internal sealed partial record SetupPage(string Html)
{
    /// <summary>
    /// The placeholder that the bundle carries where a response's nonce belongs.
    /// </summary>
    public const string NoncePlaceholder = "__NCODE_CSP_NONCE__";

    /// <summary>
    /// Replaces every nonce placeholder of the page with a nonce and inserts a <c>base</c> element after the opening
    /// <c>head</c> tag, whose URL is the setup path beneath the request's path base.
    /// </summary>
    /// <param name="nonce">The nonce of the response's Content-Security-Policy.</param>
    /// <param name="pathBase">The path base that the host is mounted under, which is empty at the site root.</param>
    /// <returns>The page that the response writes.</returns>
    public string Render(string nonce, PathString pathBase)
    {
        var html = WithBase(pathBase);
        return html.Replace(NoncePlaceholder, nonce, StringComparison.Ordinal);
    }

    // A page without a head has nowhere to insert the base element, so it keeps its own URLs.
    private string WithBase(PathString pathBase)
    {
        var head = HeadOpenTagRegex().Match(Html);
        if (!head.Success)
        {
            return Html;
        }

        var href = HtmlEncoder.Default.Encode(
            $"{pathBase.ToUriComponent()}{SetupDefaults.PathPrefix}/"
        );
        return Html.Insert(head.Index + head.Length, $"<base href=\"{href}\" />");
    }

    [GeneratedRegex(
        "<head(?:\\s(?:[^>\"']|\"[^\"]*\"|'[^']*')*)?>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex HeadOpenTagRegex();
}
