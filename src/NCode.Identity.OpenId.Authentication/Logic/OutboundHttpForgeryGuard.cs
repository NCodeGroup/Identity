#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Provides the default implementation of <see cref="IOutboundHttpForgeryGuard"/>. The disallowed-address decision is
/// the union of a hardcoded, non-weakenable floor (the IANA special-use address registry — loopback, RFC 1918 private,
/// CGNAT, link-local including the cloud instance-metadata address, unique-local, multicast, reserved, and the various
/// documentation/benchmark blocks) and every registered <see cref="IOutboundAddressFilter"/>. The floor is always
/// applied first, so a filter can only further restrict reachability; no configuration or filter can re-permit an
/// address the floor blocks.
/// </summary>
internal sealed class OutboundHttpForgeryGuard(IEnumerable<IOutboundAddressFilter> addressFilters)
    : IOutboundHttpForgeryGuard
{
    private IOutboundAddressFilter[] AddressFilters { get; } = addressFilters.ToArray();

    /// <inheritdoc />
    public async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        var endPoint = context.DnsEndPoint;

        var addresses = IPAddress.TryParse(endPoint.Host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken)
                .ConfigureAwait(false);

        var anyAllowed = false;
        Exception? lastError = null;

        foreach (var address in addresses)
        {
            if (IsDisallowedAddress(address))
                continue;

            anyAllowed = true;

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
            };

            try
            {
                await socket
                    .ConnectAsync(new IPEndPoint(address, endPoint.Port), cancellationToken)
                    .ConfigureAwait(false);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }
            catch (Exception exception)
            {
                socket.Dispose();
                lastError = exception;
            }
        }

        throw anyAllowed
            ? new HttpRequestException("Failed to connect to the requested host.", lastError)
            : new HttpRequestException(
                "The requested host did not resolve to a permitted public network address."
            );
    }

    /// <inheritdoc />
    public bool IsDisallowedAddress(IPAddress address)
    {
        if (IsFloorDisallowedAddress(address))
            return true;

        foreach (var filter in AddressFilters)
        {
            if (filter.IsDisallowed(address))
                return true;
        }

        return false;
    }

    // The always-on floor: the IANA special-use address registry (RFC 6890 and friends). This is deliberately not
    // configurable to be weaker — re-permitting any of these would be an SSRF footgun.
    private static bool IsFloorDisallowedAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsDisallowedV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsDisallowedV6(address),
            // An address family the fetch cannot vet is refused.
            _ => true,
        };
    }

    private static bool IsDisallowedV4(byte[] b) =>
        b[0] == 0 // 0.0.0.0/8 "this" network
        || b[0] == 10 // 10.0.0.0/8 private
        || (b[0] == 100 && b[1] is >= 64 and <= 127) // 100.64.0.0/10 CGNAT
        || (b[0] == 169 && b[1] == 254) // 169.254.0.0/16 link-local (incl. cloud metadata)
        || (b[0] == 172 && b[1] is >= 16 and <= 31) // 172.16.0.0/12 private
        || (b[0] == 192 && b[1] == 0 && b[2] is 0 or 2) // 192.0.0.0/24, 192.0.2.0/24 TEST-NET-1
        || (b[0] == 192 && b[1] == 168) // 192.168.0.0/16 private
        || (b[0] == 198 && b[1] is 18 or 19) // 198.18.0.0/15 benchmarking
        || (b[0] == 198 && b[1] == 51 && b[2] == 100) // 198.51.100.0/24 TEST-NET-2
        || (b[0] == 203 && b[1] == 0 && b[2] == 113) // 203.0.113.0/24 TEST-NET-3
        || b[0] >= 224; // 224.0.0.0/4 multicast, 240.0.0.0/4 reserved, 255.255.255.255 broadcast

    private static bool IsDisallowedV6(IPAddress address)
    {
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            return true;

        if (address.Equals(IPAddress.IPv6Any))
            return true;

        // fc00::/7 unique local address.
        return (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
    }
}
