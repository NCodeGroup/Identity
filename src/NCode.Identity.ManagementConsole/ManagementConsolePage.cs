// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// The console bundle's <c>index.html</c>, which each response renders with its own nonce.
/// </summary>
/// <param name="Html">The text of the bundle's <c>index.html</c>.</param>
internal sealed record ManagementConsolePage(string Html)
{
    /// <summary>
    /// Replaces every nonce placeholder of the page with a nonce.
    /// </summary>
    /// <param name="nonce">The nonce of the response's Content-Security-Policy.</param>
    /// <returns>The page that the response writes.</returns>
    public string Render(string nonce) =>
        Html.Replace(ManagementConsoleDefaults.NoncePlaceholder, nonce, StringComparison.Ordinal);
}
