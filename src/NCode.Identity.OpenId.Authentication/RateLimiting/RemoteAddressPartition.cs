// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Computes the partition value that a limit counted per remote address counts a request in: an IPv4 address as
/// itself, an IPv4-mapped IPv6 address as the IPv4 address it maps, and any other IPv6 address by its <c>/64</c>.
/// </summary>
// See ADR-1246 and ADR-1399.
internal static class RemoteAddressPartition
{
    /// <summary>
    /// Gets the partition value of a remote address.
    /// </summary>
    /// <param name="address">The remote address of the request.</param>
    /// <returns>The partition value that the address counts in.</returns>
    // An IPv6 host is usually assigned at least a /64, so one caller could otherwise spread its requests across many
    // addresses.
    public static string GetValue(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();
        return new IPAddress(bytes).ToString() + "/64";
    }
}
