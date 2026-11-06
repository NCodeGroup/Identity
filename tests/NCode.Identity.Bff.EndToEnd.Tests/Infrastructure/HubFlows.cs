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

    private const string OpenHubConnectionScript =
        "url => new Promise(resolve => { const rs = String.fromCharCode(30); const s = new WebSocket(url); window.bffHub = { closed: false }; s.onopen = () => { s.send('{\"protocol\":\"json\",\"version\":1}' + rs); window.bffHub.ping = setInterval(() => s.send('{\"type\":6}' + rs), 5000); resolve('open'); }; s.onclose = () => { window.bffHub.closed = true; clearInterval(window.bffHub.ping); resolve('closed'); }; })";

    private const string HubConnectionClosedScript = "() => window.bffHub.closed === true";

    private const float HubConnectionClosedTimeoutMilliseconds = 30_000;

    /// <summary>
    /// Opens a <c>WebSocket</c> to the URL from the page and reports <c>open</c> when the server accepted the
    /// upgrade, <c>refused</c> when the browser or the server ended it, and <c>timeout</c> when neither happened.
    /// </summary>
    public static Task<string> ProbeWebSocketAsync(IPage page, string url) =>
        page.EvaluateAsync<string>(ProbeWebSocketScript, url);

    /// <summary>
    /// Opens a <c>WebSocket</c> to the hub URL from the page, sends SignalR's JSON handshake, keeps the connection
    /// idle apart from a ping every 5 seconds, and reports <c>open</c> once the server accepted the upgrade or
    /// <c>closed</c> when the connection ended first.
    /// </summary>
    public static Task<string> OpenHubConnectionAsync(IPage page, string url) =>
        page.EvaluateAsync<string>(OpenHubConnectionScript, url);

    /// <summary>
    /// Reports whether the connection that <see cref="OpenHubConnectionAsync"/> opened in the page has closed.
    /// </summary>
    public static Task<bool> IsHubConnectionClosedAsync(IPage page) =>
        page.EvaluateAsync<bool>(HubConnectionClosedScript);

    /// <summary>
    /// Waits until the connection that <see cref="OpenHubConnectionAsync"/> opened in the page has closed.
    /// </summary>
    public static Task WaitForHubConnectionClosedAsync(IPage page) =>
        page.WaitForFunctionAsync(
            HubConnectionClosedScript,
            null,
            new PageWaitForFunctionOptions { Timeout = HubConnectionClosedTimeoutMilliseconds }
        );
}
