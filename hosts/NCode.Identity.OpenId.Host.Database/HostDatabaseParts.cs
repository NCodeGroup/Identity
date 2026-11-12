// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// The parts of the Postgres connection that the <c>OpenIdDatabase</c> configuration section binds to. The section
/// binds strictly, so a key that is not one of these properties stops the host.
/// </summary>
// See ADR-1509.
internal sealed class HostDatabaseParts
{
    /// <summary>
    /// The host name or address of the Postgres server.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// The TCP port of the Postgres server, a number from 1 to 65535.
    /// </summary>
    public string? Port { get; set; }

    /// <summary>
    /// The name of the database.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The user name.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The password.
    /// </summary>
    public string? Password { get; set; }
}
