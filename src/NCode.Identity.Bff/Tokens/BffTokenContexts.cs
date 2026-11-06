// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;

namespace NCode.Identity.Bff.Tokens;

/// <summary>
/// Creates the <see cref="IBffSessionScope"/> that a token is obtained through.
/// </summary>
/// <param name="frontends">The catalog of frontends.</param>
/// <param name="resolver">Resolves the session of a request.</param>
/// <param name="cookies">Deletes the session cookie of a request.</param>
internal class BffTokenContexts(
    BffFrontendCatalog frontends,
    BffSessionResolver resolver,
    BffCookies cookies
)
{
    private BffFrontendCatalog Frontends { get; } = frontends;
    private BffSessionResolver Resolver { get; } = resolver;
    private BffCookies Cookies { get; } = cookies;

    /// <summary>
    /// Creates the session scope of a request to an endpoint that the BFF guards. A request to a hub whose principal
    /// carries a connection session is served by that connection, so it never writes a cookie to an upgraded response.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    public virtual IBffSessionScope ForRequest(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (
            BffGuardedEndpoints.IsHub(httpContext.GetEndpoint())
            && httpContext.User.GetBffConnectionSession() is IBffSessionScope connection
        )
        {
            return connection;
        }

        return new BffRequestTokenContext(httpContext, Frontends, Resolver, Cookies);
    }
}
