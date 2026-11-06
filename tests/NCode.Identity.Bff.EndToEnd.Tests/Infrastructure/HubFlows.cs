// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// The WebSocket interactions that the hub tests share.
/// </summary>
public static class HubFlows
{
    private const string ProbeWebSocketScript =
        "url => new Promise(resolve => { const s = new WebSocket(url); const t = setTimeout(() => { s.close(); resolve('timeout'); }, 10000); s.onopen = () => { clearTimeout(t); s.close(); resolve('open'); }; s.onerror = () => { clearTimeout(t); resolve('refused'); }; s.onclose = () => { clearTimeout(t); resolve('refused'); }; })";

    /// <summary>
    /// Opens a <c>WebSocket</c> to the URL from the page and reports <c>open</c> when the server accepted the
    /// upgrade, <c>refused</c> when the browser or the server ended it, and <c>timeout</c> when neither happened.
    /// </summary>
    public static Task<string> ProbeWebSocketAsync(IPage page, string url) =>
        page.EvaluateAsync<string>(ProbeWebSocketScript, url);
}
