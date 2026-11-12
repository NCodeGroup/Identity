// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.AspNetCore.TransportSecurity;

/// <summary>
/// Judges the addresses a server bound: at least one must be bound, and each must be an HTTPS endpoint on a TCP port.
/// A plain-HTTP endpoint, a Unix socket and a named pipe are refused.
/// </summary>
internal static class TransportEndpointInspector
{
    /// <summary>
    /// Sorts the bound addresses into the HTTPS endpoints on a TCP port and the failures.
    /// </summary>
    /// <param name="addresses">The addresses that the server bound.</param>
    /// <returns>The failures and the HTTPS endpoints to probe.</returns>
    internal static TransportEndpointInspection Inspect(IEnumerable<string> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        var failures = new List<string>();
        var tlsEndpoints = new List<Uri>();
        var count = 0;

        foreach (var address in addresses)
        {
            count++;

            if (
                Uri.TryCreate(address, UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && uri.Host != "unix"
                && uri.Port > 0
            )
            {
                tlsEndpoints.Add(uri);
            }
            else if (uri is not null && uri.Scheme == Uri.UriSchemeHttp)
            {
                failures.Add($"The server serves plain HTTP at '{address}'.");
            }
            else
            {
                failures.Add(
                    $"The server binds '{address}', which is not an HTTPS endpoint on a TCP port."
                );
            }
        }

        if (count == 0)
            failures.Add("The server binds no endpoint.");

        return new TransportEndpointInspection(failures, tlsEndpoints);
    }
}
