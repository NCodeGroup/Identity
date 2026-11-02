// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Provides the shared server-side request forgery (SSRF) network floor for every server-initiated outbound fetch of
/// a client-supplied URL (for example <c>sector_identifier_uri</c>, <c>jwks_uri</c>, and the JAR <c>request_uri</c>).
/// An implementation is wired as the <see cref="SocketsHttpHandler.ConnectCallback"/> of the named HTTP client each
/// fetch uses, so every connection attempt — including one reached through a DNS answer or a redirect — is pinned to a
/// resolved IP address and rejected when that address is not a public, globally-routable unicast address.
/// </summary>
internal interface IOutboundHttpForgeryGuard
{
    /// <summary>
    /// Connects to the requested host only when it resolves to a permitted public address, defeating SSRF attempts
    /// (literal private IPs, DNS rebinding, and redirect-to-internal) by pinning the socket to the vetted address.
    /// </summary>
    ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Determines whether the supplied address is one the fetch must refuse to connect to: an address blocked by the
    /// always-on floor (loopback, private, link-local, unique-local, multicast, reserved, or otherwise
    /// non-public-unicast) or by any registered <see cref="IOutboundAddressFilter"/>.
    /// </summary>
    bool IsDisallowedAddress(IPAddress address);
}
