// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Playwright;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// The browser interactions that the end-to-end tests share.
/// </summary>
public static class BrowserFlows
{
    private const string FetchScript =
        "async ([url, withHeader]) => { try { const r = await fetch(url, { credentials: 'include', headers: withHeader ? { 'X-CSRF': '1' } : {} }); return JSON.stringify({ status: r.status, body: await r.text(), blocked: false }); } catch { return JSON.stringify({ status: 0, body: '', blocked: true }); } }";

    private const string FollowLinkScript =
        "url => { const a = document.createElement('a'); a.href = url; document.body.appendChild(a); a.click(); }";

    private const string DocumentCompleteScript = "() => document.readyState === 'complete'";

    private const string SubmitFormScript =
        "([action, fields]) => { const f = document.createElement('form'); f.method = 'post'; f.action = action; for (const [name, value] of Object.entries(fields)) { const i = document.createElement('input'); i.type = 'hidden'; i.name = name; i.value = value; f.appendChild(i); } document.body.appendChild(f); f.submit(); }";

    /// <summary>
    /// Signs the user in at Keycloak through the BFF's login endpoint and waits for the return page.
    /// </summary>
    public static async Task LoginAsync(IPage page, BffApplication bff, KeycloakProvider keycloak)
    {
        await page.GotoAsync($"{bff.Origin}/bff/login?returnUrl=%2F");
        await page.FillAsync("#username", KeycloakProvider.UserName);
        await page.FillAsync("#password", keycloak.UserPassword);
        await page.ClickAsync("#kc-login");
        await page.WaitForURLAsync(
            $"{bff.Origin}/",
            new PageWaitForURLOptions { WaitUntil = WaitUntilState.Commit }
        );
        await WaitForDocumentAsync(page);
    }

    /// <summary>
    /// Calls <c>fetch</c> in the page, with credentials, and reports what the browser let through.
    /// </summary>
    public static async Task<FetchOutcome> FetchAsync(
        IPage page,
        string url,
        bool withAntiforgeryHeader
    )
    {
        var json = await page.EvaluateAsync<string>(
            FetchScript,
            new object[] { url, withAntiforgeryHeader }
        );
        return JsonSerializer.Deserialize<FetchOutcome>(json, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The fetch script returned null.");
    }

    /// <summary>
    /// Opens a new page in the context at the root of an origin.
    /// </summary>
    public static async Task<IPage> OpenOriginAsync(IBrowserContext context, string origin)
    {
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{origin}/");
        return page;
    }

    /// <summary>
    /// Clicks a link to the URL, waits for the navigation and returns the text of the page that loads.
    /// </summary>
    public static async Task<string> FollowLinkAsync(IPage page, string url)
    {
#pragma warning disable CS0612 // Playwright marks it obsolete without a replacement that waits for whatever URL the redirects reach.
        await page.RunAndWaitForNavigationAsync(
            () => page.EvaluateAsync(FollowLinkScript, url),
            new PageRunAndWaitForNavigationOptions { WaitUntil = WaitUntilState.Commit }
        );
#pragma warning restore CS0612
        await WaitForDocumentAsync(page);
        return await page.InnerTextAsync("body");
    }

    /// <summary>
    /// Submits a <c>POST</c> form of hidden fields to the action and waits for the response to that <c>POST</c>.
    /// </summary>
    public static async Task SubmitFormAsync(
        IPage page,
        string action,
        IReadOnlyDictionary<string, string> fields
    )
    {
        await page.RunAndWaitForResponseAsync(
            () => page.EvaluateAsync(SubmitFormScript, new object[] { action, fields }),
            response => response.Request.Method == "POST" && response.Url == action
        );
    }

    /// <summary>
    /// Submits a <c>POST</c> form of hidden fields to the action and waits until the navigation that its redirects
    /// start has loaded a URL that the destination predicate accepts.
    /// </summary>
    public static async Task SubmitFormAndWaitForUrlAsync(
        IPage page,
        string action,
        IReadOnlyDictionary<string, string> fields,
        Func<string, bool> destination
    )
    {
#pragma warning disable CS0612 // Playwright marks it obsolete without a replacement that waits for a navigation the page has not started yet.
        await page.RunAndWaitForNavigationAsync(
            () => page.EvaluateAsync(SubmitFormScript, new object[] { action, fields }),
            new PageRunAndWaitForNavigationOptions
            {
                UrlFunc = destination,
                WaitUntil = WaitUntilState.Commit,
            }
        );
#pragma warning restore CS0612
        await WaitForDocumentAsync(page);
    }

    // Firefox can finish loading a page that a cross-origin redirect chain reaches without Playwright seeing its load
    // event, so a navigation is awaited until it commits and the document's own ready state says that it loaded.
    private static async Task WaitForDocumentAsync(IPage page) =>
        await page.WaitForFunctionAsync(DocumentCompleteScript);
}
