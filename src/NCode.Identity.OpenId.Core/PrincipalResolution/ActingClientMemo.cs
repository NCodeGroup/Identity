// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Holds, in the <see cref="HttpContext.Items"/> of a request, the client each caller of the request acts as, or none,
/// so a caller is resolved once per request. The memo ends with the request.
/// </summary>
// See ADR-1470.
internal sealed class ActingClientMemo
{
    private static object ItemKey { get; } = new();

    /// <summary>
    /// Gets the memo of a request, storing an empty memo on the request first when it has none.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> of the request.</param>
    /// <returns>The <see cref="ActingClientMemo"/> of the request.</returns>
    public static ActingClientMemo Get(HttpContext httpContext)
    {
        if (
            httpContext.Items.TryGetValue(ItemKey, out var stored)
            && stored is ActingClientMemo existing
        )
        {
            return existing;
        }

        var memo = new ActingClientMemo();
        httpContext.Items[ItemKey] = memo;
        return memo;
    }

    private Dictionary<ActingCaller, PersistedClient?> Clients { get; } = [];

    /// <summary>
    /// Gets the client that a caller was remembered to act as.
    /// </summary>
    /// <param name="caller">The <see cref="ActingCaller"/> to look up.</param>
    /// <param name="client">
    /// When this method returns <c>true</c>, the client that the caller acts as, or <c>null</c> when the caller acts
    /// as none.
    /// </param>
    /// <returns><c>true</c> when the caller was remembered; otherwise, <c>false</c>.</returns>
    public bool TryRecall(ActingCaller caller, out PersistedClient? client) =>
        Clients.TryGetValue(caller, out client);

    /// <summary>
    /// Remembers the client that a caller acts as, or that it acts as none.
    /// </summary>
    /// <param name="caller">The <see cref="ActingCaller"/> to remember.</param>
    /// <param name="client">The client that the caller acts as, or <c>null</c> when the caller acts as none.</param>
    public void Remember(ActingCaller caller, PersistedClient? client) => Clients[caller] = client;
}
