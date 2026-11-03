// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Holds the counts of failed client authentications that the attempt limiter read or made for one request in one
/// window, so the handlers that each examine the request share one read of each counter. The state lives in the
/// request's items, so it ends with the request.
/// </summary>
/// <param name="windowStart">The start of the window the counts belong to.</param>
// See ADR-1399.
internal sealed class ClientAuthenticationAttemptState(DateTimeOffset windowStart)
{
    /// <summary>
    /// Gets the start of the window the counts belong to.
    /// </summary>
    public DateTimeOffset WindowStart { get; } = windowStart;

    /// <summary>
    /// Gets or sets the count of the request's remote address, or <see langword="null"/> before it is read.
    /// </summary>
    public long? AddressCount { get; set; }

    /// <summary>
    /// Gets the counts of the request's remote address with each <c>client_id</c> read so far.
    /// </summary>
    public Dictionary<string, long> ClientCounts { get; } = new(StringComparer.Ordinal);
}
