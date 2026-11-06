// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Creates the <see cref="IBffConnectionSession"/> that a long-lived connection holds.
/// </summary>
/// <param name="resolver">Loads and validates the session.</param>
/// <param name="frontends">The frontends that a connection looks its frontend up in on every use.</param>
internal class BffConnectionSessions(BffSessionResolver resolver, BffFrontendCatalog frontends)
{
    private BffSessionResolver Resolver { get; } = resolver;
    private BffFrontendCatalog Frontends { get; } = frontends;

    /// <summary>
    /// Creates the connection session of a session handle.
    /// </summary>
    /// <param name="frontend">The frontend that the session belongs to.</param>
    /// <param name="handle">The session handle.</param>
    /// <param name="logoutUrl">The local URL of the frontend's logout endpoint.</param>
    public virtual IBffConnectionSession Create(
        BffFrontend frontend,
        string handle,
        string logoutUrl
    )
    {
        ArgumentNullException.ThrowIfNull(frontend);
        ArgumentException.ThrowIfNullOrEmpty(handle);
        ArgumentNullException.ThrowIfNull(logoutUrl);

        return new DefaultBffConnectionSession(
            frontend.Name,
            handle,
            logoutUrl,
            Resolver,
            Frontends
        );
    }
}
