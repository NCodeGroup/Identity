// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Provides the default implementation of <see cref="IOutboundHostResolver"/>, which asks the system resolver.
/// </summary>
// A thin wrapper over the system resolver.
[ExcludeFromCodeCoverage]
internal class DefaultOutboundHostResolver : IOutboundHostResolver
{
    /// <inheritdoc />
    public async ValueTask<IPAddress[]> ResolveAsync(
        string host,
        CancellationToken cancellationToken
    ) => await Dns.GetHostAddressesAsync(host, cancellationToken);
}
