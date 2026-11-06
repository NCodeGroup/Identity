// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// The <see cref="IBffSessionScope"/> of a request to an endpoint that the BFF guards.
/// </summary>
/// <param name="httpContext">The request.</param>
/// <param name="frontends">The catalog of frontends.</param>
/// <param name="resolver">Resolves the session of the request.</param>
/// <param name="cookies">Deletes the session cookie of the request.</param>
internal sealed class BffRequestTokenContext(
    HttpContext httpContext,
    BffFrontendCatalog frontends,
    BffSessionResolver resolver,
    BffCookies cookies
) : IBffSessionScope
{
    private readonly BffFrontendCatalog _frontends = frontends;
    private readonly BffSessionResolver _resolver = resolver;
    private readonly BffCookies _cookies = cookies;

    /// <inheritdoc />
    public string? Frontend => SelectedFrontend?.Name;

    /// <inheritdoc />
    public HttpContext HttpContext { get; } = httpContext;

    /// <inheritdoc />
    public BffFrontend? SelectedFrontend => _frontends.Resolve(HttpContext);

    /// <inheritdoc />
    public ValueTask<BffSessionState?> ResolveAsync(CancellationToken cancellationToken) =>
        _resolver.ResolveAsync(HttpContext, cancellationToken);

    /// <inheritdoc />
    public void OnSessionEnded()
    {
        if (SelectedFrontend is { } frontend)
            _cookies.DeleteSession(HttpContext.Response, frontend);
    }
}
