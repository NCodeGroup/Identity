// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Configuration;
using NCode.Identity.Registration;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Builds the StackExchange.Redis <see cref="ConfigurationOptions"/> from the parts of a Valkey connection, the
/// <c>ValkeyConnection</c> configuration section, which is the parts form of the <c>ConnectionStrings:valkey</c>
/// connection string. The section binds strictly, so a key that is not one of the parts stops the host.
/// </summary>
/// <remarks>
/// The section holds <c>Host</c>, <c>Port</c> (6379 when omitted), <c>Username</c>, <c>Password</c> and <c>Ssl</c>, the
/// values a Valkey operator writes to its Secret. The options are built from the parts rather than from a finished
/// connection string, so a password that holds <c>,</c> or <c>=</c> stays a password.
/// </remarks>
// See ADR-1510.
internal static class ValkeyConnectionParts
{
    /// <summary>
    /// The name of the configuration section that holds the connection's parts.
    /// </summary>
    internal const string SectionName = "ValkeyConnection";

    private const int DefaultPort = 6379;
    private const string HostPart = nameof(ValkeyConnectionPartsOptions.Host);
    private const string PortPart = nameof(ValkeyConnectionPartsOptions.Port);
    private const string SslPart = nameof(ValkeyConnectionPartsOptions.Ssl);

    /// <summary>
    /// Builds the <see cref="ConfigurationOptions"/> from the <c>ValkeyConnection</c> section of
    /// <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The configuration to read.</param>
    /// <returns>The options, or <see langword="null"/> when no part is configured.</returns>
    /// <exception cref="InvalidOperationException">
    /// The section holds a key that is not one of the parts, a part is configured without the host, the port is not a
    /// number from 1 to 65535, or <c>Ssl</c> is not a boolean. The message names the configuration paths and never holds a configured value.
    /// </exception>
    internal static ConfigurationOptions? Resolve(IConfiguration configuration)
    {
        var parts = new ValkeyConnectionPartsOptions();
        configuration.GetSection(SectionName).BindStrictly(parts);

        if (
            !new[] { parts.Host, parts.Port, parts.Username, parts.Password, parts.Ssl }.Any(part =>
                !string.IsNullOrEmpty(part)
            )
        )
            return null;

        var host = parts.Host;
        if (string.IsNullOrEmpty(host))
        {
            throw new InvalidOperationException(
                $"The '{SectionName}' parts are incomplete; set {SectionName}:{HostPart}."
            );
        }

        var port = DefaultPort;
        var portValue = parts.Port;
        if (
            !string.IsNullOrEmpty(portValue)
            && (
                !int.TryParse(portValue, NumberStyles.None, CultureInfo.InvariantCulture, out port)
                || port is < 1 or > 65535
            )
        )
        {
            throw new InvalidOperationException(
                $"The '{SectionName}:{PortPart}' value is not a port number from 1 to 65535."
            );
        }

        var ssl = false;
        var sslValue = parts.Ssl;
        if (!string.IsNullOrEmpty(sslValue) && !bool.TryParse(sslValue, out ssl))
        {
            throw new InvalidOperationException(
                $"The '{SectionName}:{SslPart}' value is not a boolean."
            );
        }

        var options = new ConfigurationOptions { Ssl = ssl };
        options.EndPoints.Add(host, port);

        if (!string.IsNullOrEmpty(parts.Username))
            options.User = parts.Username;

        if (!string.IsNullOrEmpty(parts.Password))
            options.Password = parts.Password;

        return options;
    }
}
