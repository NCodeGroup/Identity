// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Resolves the reference OP host's Postgres connection string from configuration. The <c>api</c> host and the
/// <c>MigrationService</c> worker both resolve it here, so the two forms of one connection are judged in one place.
/// </summary>
/// <remarks>
/// The connection is configured as exactly one of two forms: a finished connection string under
/// <c>ConnectionStrings:openiddb</c> (what Aspire supplies), or the parts under the <c>OpenIdDatabase</c> section
/// (<c>Host</c>, <c>Port</c>, <c>Name</c>, <c>Username</c> and <c>Password</c>, the values a Postgres operator writes
/// to its Secret). The parts are assembled with <see cref="NpgsqlConnectionStringBuilder"/>, so a password that holds
/// <c>;</c>, <c>'</c> or <c>"</c> stays a password.
/// </remarks>
// See ADR-1509.
[PublicAPI]
public static class HostDatabaseConnection
{
    /// <summary>
    /// The name of the connection string, <c>ConnectionStrings:openiddb</c>.
    /// </summary>
    public const string ConnectionStringName = "openiddb";

    /// <summary>
    /// The name of the configuration section that holds the connection's parts.
    /// </summary>
    public const string SectionName = "OpenIdDatabase";

    private const string ConnectionStringsPath = $"ConnectionStrings:{ConnectionStringName}";
    private const string HostPart = "Host";
    private const string PortPart = "Port";
    private const string NamePart = "Name";
    private const string UsernamePart = "Username";
    private const string PasswordPart = "Password";

    private static string[] RequiredParts { get; } =
    [HostPart, NamePart, UsernamePart, PasswordPart];

    /// <summary>
    /// Resolves the Postgres connection string from <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <returns>The connection string, or <see langword="null"/> when neither form is configured.</returns>
    /// <exception cref="InvalidOperationException">
    /// Both forms are configured, a required part is missing, or the port is not a number from 1 to 65535. The message
    /// names the configuration paths and never holds a configured value.
    /// </exception>
    public static string? Resolve(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        var section = configuration.GetSection(SectionName);

        var hasConnectionString = !string.IsNullOrEmpty(connectionString);
        var hasParts = RequiredParts
            .Append(PortPart)
            .Any(part => !string.IsNullOrEmpty(section[part]));

        if (hasConnectionString && hasParts)
        {
            throw new InvalidOperationException(
                $"The connection string '{ConnectionStringsPath}' and the '{SectionName}' parts are both configured; "
                    + "configure exactly one of them."
            );
        }

        if (!hasParts)
            return hasConnectionString ? connectionString : null;

        var missing = RequiredParts
            .Where(part => string.IsNullOrEmpty(section[part]))
            .Select(part => $"{SectionName}:{part}")
            .ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"The '{SectionName}' parts are incomplete; set {string.Join(", ", missing)}."
            );
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = section[HostPart],
            Database = section[NamePart],
            Username = section[UsernamePart],
            Password = section[PasswordPart],
        };

        var port = section[PortPart];
        if (!string.IsNullOrEmpty(port))
        {
            if (
                !int.TryParse(
                    port,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var portNumber
                ) || portNumber is < 1 or > 65535
            )
            {
                throw new InvalidOperationException(
                    $"The '{SectionName}:{PortPart}' value is not a port number from 1 to 65535."
                );
            }

            builder.Port = portNumber;
        }

        return builder.ConnectionString;
    }
}
