// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Represents the identity of a BFF session principal whose request opened a long-lived connection, and that carries
/// the <see cref="IBffConnectionSession"/> the connection holds.
/// </summary>
[PublicAPI]
public sealed class BffConnectionIdentity : ClaimsIdentity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffConnectionIdentity"/> class from an existing identity.
    /// </summary>
    /// <param name="identity">The identity whose claims and authentication type are copied.</param>
    /// <param name="session">The session that the connection holds.</param>
    public BffConnectionIdentity(ClaimsIdentity identity, IBffConnectionSession session)
        : base(RequireIdentity(identity))
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
    }

    /// <summary>
    /// Gets the session that the connection holds.
    /// </summary>
    public IBffConnectionSession Session { get; }

    /// <summary>
    /// Creates a copy of the identity that carries the claims and never the session.
    /// </summary>
    /// <returns>A <see cref="ClaimsIdentity"/> that is not a <see cref="BffConnectionIdentity"/>.</returns>
    public override ClaimsIdentity Clone() => new ClaimsIdentity(this);

    private static ClaimsIdentity RequireIdentity(ClaimsIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return identity;
    }
}
