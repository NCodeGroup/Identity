// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Tokens;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// The BFF session that a long-lived connection holds. The session handle lives only in this object, in memory, for
/// the connection's life.
/// </summary>
/// <param name="frontend">The frontend that the session belongs to.</param>
/// <param name="handle">The session handle.</param>
/// <param name="logoutUrl">The local URL of the frontend's logout endpoint.</param>
/// <param name="resolver">Loads and validates the session.</param>
internal sealed class DefaultBffConnectionSession(
    BffFrontend frontend,
    string handle,
    string logoutUrl,
    BffSessionResolver resolver
) : IBffConnectionSession, IBffSessionScope
{
    private BffFrontend SessionFrontend { get; } = frontend;
    private string Handle { get; } = handle;
    private string LogoutUrl { get; } = logoutUrl;
    private BffSessionResolver Resolver { get; } = resolver;

    /// <inheritdoc />
    public string? Frontend => SessionFrontend.Name;

    /// <inheritdoc />
    public HttpContext? HttpContext => null;

    /// <inheritdoc />
    public BffFrontend? SelectedFrontend => SessionFrontend;

    /// <inheritdoc />
    public TimeSpan RevalidationInterval => SessionFrontend.Options.Session.RevalidationInterval;

    /// <inheritdoc />
    public ValueTask<BffSessionState?> ResolveAsync(CancellationToken cancellationToken) =>
        Resolver.ValidateAsync(SessionFrontend, Handle, null, null, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<ClaimsPrincipal?> ValidateAsync(
        DateTimeOffset? lastActivityAt,
        CancellationToken cancellationToken
    )
    {
        var state = await Resolver.ValidateAsync(
            SessionFrontend,
            Handle,
            null,
            lastActivityAt,
            cancellationToken
        );
        return state is null ? null : BffSessionPrincipal.Create(state, LogoutUrl);
    }

    /// <inheritdoc />
    public void OnSessionEnded() { }
}
