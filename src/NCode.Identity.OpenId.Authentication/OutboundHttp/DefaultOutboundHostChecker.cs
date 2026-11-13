// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Sockets;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Provides the default implementation of <see cref="IOutboundHostChecker"/>. It asks the same
/// <see cref="IOutboundHttpForgeryGuard.IsDisallowedAddress"/> the outbound fetch uses, so the answer matches what a
/// fetch of the host would do.
/// </summary>
internal class DefaultOutboundHostChecker(
    IOutboundHttpForgeryGuard forgeryGuard,
    IOutboundHostResolver hostResolver
) : IOutboundHostChecker
{
    /// <inheritdoc />
    public async ValueTask<bool> IsRefusedAsync(string host, CancellationToken cancellationToken)
    {
        // See ADR-1106.
        if (IPAddress.TryParse(host, out var literal))
            return forgeryGuard.IsDisallowedAddress(literal);

        IPAddress[] addresses;
        try
        {
            addresses = await hostResolver.ResolveAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            // A name that does not resolve is not refused.
            return false;
        }

        return addresses.Length > 0 && addresses.All(forgeryGuard.IsDisallowedAddress);
    }
}
