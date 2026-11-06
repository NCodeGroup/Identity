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
    private readonly BffFrontend _frontend = frontend;
    private readonly string _handle = handle;
    private readonly string _logoutUrl = logoutUrl;
    private readonly BffSessionResolver _resolver = resolver;

    /// <inheritdoc />
    public string? Frontend => _frontend.Name;

    /// <inheritdoc />
    public HttpContext? HttpContext => null;

    /// <inheritdoc />
    public BffFrontend? SelectedFrontend => _frontend;

    /// <inheritdoc />
    public TimeSpan RevalidationInterval => _frontend.Options.Session.RevalidationInterval;

    /// <inheritdoc />
    public ValueTask<BffSessionState?> ResolveAsync(CancellationToken cancellationToken) =>
        _resolver.ValidateAsync(_frontend, _handle, null, null, cancellationToken);

    /// <inheritdoc />
    public async ValueTask<ClaimsPrincipal?> ValidateAsync(
        DateTimeOffset? lastActivityAt,
        CancellationToken cancellationToken
    )
    {
        var state = await _resolver.ValidateAsync(
            _frontend,
            _handle,
            null,
            lastActivityAt,
            cancellationToken
        );
        return state is null ? null : BffSessionPrincipal.Create(state, _logoutUrl);
    }

    /// <inheritdoc />
    public void OnSessionEnded() { }
}
