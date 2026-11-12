// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using NCode.Identity.Registration;
using Npgsql;

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Resolves the reference OP host's Postgres connection string from configuration. The application and its migration
/// worker both resolve it here, so the two forms of one connection are judged in one place.
/// </summary>
/// <remarks>
/// The connection is configured as exactly one of two forms: a finished connection string under
/// <c>ConnectionStrings:openiddb</c> (what Aspire supplies), or the parts under the <c>OpenIdDatabase</c> section
/// (<c>Host</c>, <c>Port</c>, <c>Name</c>, <c>Username</c> and <c>Password</c>, the values a Postgres operator writes
/// to its Secret). The parts are assembled with <see cref="NpgsqlConnectionStringBuilder"/>, so a password that holds
/// <c>;</c>, <c>'</c> or <c>"</c> stays a password. The section binds strictly, so a key that is not one of the parts
/// stops the host.
/// </remarks>
// See ADR-1509. The application is the OpenId host's api project and the migration worker is the MigrationService
// project.
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
    private const string PortPart = "Port";

    /// <summary>
    /// Resolves the Postgres connection string from <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <returns>The connection string, or <see langword="null"/> when neither form is configured.</returns>
    /// <exception cref="InvalidOperationException">
    /// Both forms are configured, the section holds a key that is not one of the parts, a required part is missing, or
    /// the port is not a number from 1 to 65535. The message names the configuration paths and never holds a
    /// configured value.
    /// </exception>
    public static string? Resolve(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        var parts = new HostDatabaseParts();
        configuration.GetSection(SectionName).BindStrictly(parts);

        var hasConnectionString = !string.IsNullOrEmpty(connectionString);
        var hasParts = new[]
        {
            parts.Host,
            parts.Port,
            parts.Name,
            parts.Username,
            parts.Password,
        }.Any(part => !string.IsNullOrEmpty(part));

        if (hasConnectionString && hasParts)
        {
            throw new InvalidOperationException(
                $"The connection string '{ConnectionStringsPath}' and the '{SectionName}' parts are both configured; "
                    + "configure exactly one of them."
            );
        }

        if (!hasParts)
            return hasConnectionString ? connectionString : null;

        var missing = new List<string>();
        if (string.IsNullOrEmpty(parts.Host))
            missing.Add($"{SectionName}:{nameof(HostDatabaseParts.Host)}");
        if (string.IsNullOrEmpty(parts.Name))
            missing.Add($"{SectionName}:{nameof(HostDatabaseParts.Name)}");
        if (string.IsNullOrEmpty(parts.Username))
            missing.Add($"{SectionName}:{nameof(HostDatabaseParts.Username)}");
        if (string.IsNullOrEmpty(parts.Password))
            missing.Add($"{SectionName}:{nameof(HostDatabaseParts.Password)}");
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"The '{SectionName}' parts are incomplete; set {string.Join(", ", missing)}."
            );
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = parts.Host,
            Database = parts.Name,
            Username = parts.Username,
            Password = parts.Password,
        };

        if (!string.IsNullOrEmpty(parts.Port))
        {
            if (
                !int.TryParse(
                    parts.Port,
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
