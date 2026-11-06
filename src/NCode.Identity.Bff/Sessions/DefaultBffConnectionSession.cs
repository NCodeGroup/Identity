// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Tokens;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The BFF session that a long-lived connection holds. The session handle lives only in this object, in memory, for
/// the connection's life. The connection looks its frontend up by name on every use, so a frontend that a
/// configuration change removes ends the session at the next revalidation. Once a use finds the session ended, the
/// connection cancels its SessionEnded token and reports the session ended without reading the store.
/// </summary>
/// <param name="frontend">The name of the frontend that the session belongs to.</param>
/// <param name="handle">The session handle.</param>
/// <param name="logoutUrl">The local URL of the frontend's logout endpoint.</param>
/// <param name="resolver">Loads and validates the session.</param>
/// <param name="frontends">The frontends that the connection's frontend is looked up in.</param>
// See ADR-1442.
internal sealed class DefaultBffConnectionSession(
    string frontend,
    string handle,
    string logoutUrl,
    BffSessionResolver resolver,
    BffFrontendCatalog frontends
) : IBffConnectionSession, IBffSessionScope
{
    private string FrontendName { get; } = frontend;
    private string Handle { get; } = handle;
    private string LogoutUrl { get; } = logoutUrl;
    private BffSessionResolver Resolver { get; } = resolver;
    private BffFrontendCatalog Frontends { get; } = frontends;
    private CancellationTokenSource EndedSource { get; } = new();

    /// <inheritdoc />
    public string? Frontend => FrontendName;

    /// <inheritdoc />
    public HttpContext? HttpContext => null;

    /// <inheritdoc />
    public BffFrontend? SelectedFrontend =>
        Frontends.TryGet(FrontendName, out var found) ? found : null;

    /// <inheritdoc />
    public TimeSpan RevalidationInterval =>
        SelectedFrontend?.Options.Session.RevalidationInterval ?? TimeSpan.Zero;

    /// <inheritdoc />
    public CancellationToken SessionEnded => EndedSource.Token;

    /// <inheritdoc />
    public ValueTask<BffSessionState?> ResolveAsync(CancellationToken cancellationToken) =>
        LoadAsync(null, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<ClaimsPrincipal?> ValidateAsync(
        DateTimeOffset? lastActivityAt,
        CancellationToken cancellationToken
    )
    {
        var state = await LoadAsync(lastActivityAt, cancellationToken);
        return state is null ? null : BffSessionPrincipal.Create(state, LogoutUrl);
    }

    /// <inheritdoc />
    public void OnSessionEnded() => MarkEnded();

    private async ValueTask<BffSessionState?> LoadAsync(
        DateTimeOffset? lastActivityAt,
        CancellationToken cancellationToken
    )
    {
        if (EndedSource.IsCancellationRequested)
            return null;

        var state = SelectedFrontend is { } selected
            ? await Resolver.ValidateAsync(
                selected,
                Handle,
                null,
                lastActivityAt,
                cancellationToken
            )
            : null;

        if (state is null)
            MarkEnded();

        return state;
    }

    private void MarkEnded() => _ = EndedSource.CancelAsync();
}
