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
