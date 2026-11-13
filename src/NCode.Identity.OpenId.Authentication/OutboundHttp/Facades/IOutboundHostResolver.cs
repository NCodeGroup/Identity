// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp.Facades;

/// <summary>
/// Resolves a host name to the addresses an outbound fetch would connect to.
/// </summary>
internal interface IOutboundHostResolver
{
    /// <summary>
    /// Resolves the specified <paramref name="host"/> name to its addresses.
    /// </summary>
    /// <param name="host">The host name to resolve.</param>
    /// <param name="cancellationToken">A token to cancel the resolution.</param>
    /// <returns>The addresses the host resolves to.</returns>
    ValueTask<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
