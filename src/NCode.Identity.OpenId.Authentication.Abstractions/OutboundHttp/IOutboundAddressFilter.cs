// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Extension point for the server-initiated outbound-fetch SSRF guard. Every registered filter is consulted
/// <b>in addition to</b> the always-on, non-weakenable floor that blocks loopback, private, link-local, unique-local,
/// multicast, reserved, and other non-public-unicast addresses (the IANA special-use registry). A filter may only
/// <b>further restrict</b> which addresses are reachable — returning <see langword="true"/> blocks an additional
/// address; returning <see langword="false"/> never re-permits an address the floor (or another filter) already
/// blocked. Register additional filters (for example to block extra corporate CIDRs) via <c>TryAddEnumerable</c>.
/// </summary>
[PublicAPI]
public interface IOutboundAddressFilter
{
    /// <summary>
    /// Determines whether the outbound fetch must refuse to connect to the supplied resolved address, blocking it in
    /// addition to the always-on floor.
    /// </summary>
    /// <param name="address">The resolved destination <see cref="IPAddress"/> the fetch is about to connect to.</param>
    /// <returns><see langword="true"/> to block the address; otherwise, <see langword="false"/>. Returning
    /// <see langword="false"/> does not re-permit an address blocked by the floor or another filter.</returns>
    bool IsDisallowed(IPAddress address);
}
