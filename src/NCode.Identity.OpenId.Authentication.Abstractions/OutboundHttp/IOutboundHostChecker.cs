// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Determines, ahead of any fetch, whether the server-initiated outbound-fetch SSRF guard would refuse to connect to
/// a host. The decision is the one the fetch itself makes: a host is refused when the always-on floor or a registered
/// <see cref="IOutboundAddressFilter"/> blocks every address it resolves to. A caller uses it to reject a URL that can
/// never be fetched when it is stored, rather than when it is first used.
/// </summary>
[PublicAPI]
public interface IOutboundHostChecker
{
    /// <summary>
    /// Determines whether the outbound fetch would refuse to connect to the supplied host.
    /// </summary>
    /// <param name="host">The host name or literal IP address of the URL to be fetched.</param>
    /// <param name="cancellationToken">A token to cancel name resolution.</param>
    /// <returns><see langword="true"/> when <paramref name="host"/> is a literal IP address the guard refuses, or a name
    /// that resolves and whose every resolved address the guard refuses. <see langword="false"/> when at least one
    /// address is permitted, and also when the name does not resolve: a URL may be registered before its DNS record
    /// exists, so an unresolvable name is not refused.</returns>
    ValueTask<bool> IsRefusedAsync(string host, CancellationToken cancellationToken);
}
