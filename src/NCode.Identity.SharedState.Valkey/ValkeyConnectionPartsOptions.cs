// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// The parts of the Valkey connection that the <c>ValkeyConnection</c> configuration section binds to. The section
/// binds strictly, so a key that is not one of these properties stops the host.
/// </summary>
// See ADR-1510.
internal sealed class ValkeyConnectionPartsOptions
{
    /// <summary>
    /// The host name or address of the Valkey server.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// The TCP port of the Valkey server, a number from 1 to 65535. The port is 6379 when omitted.
    /// </summary>
    public string? Port { get; set; }

    /// <summary>
    /// The user name.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Whether the connection uses TLS, <c>true</c> or <c>false</c>.
    /// </summary>
    public string? Ssl { get; set; }
}
