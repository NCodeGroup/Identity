// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Creates, loads, updates and ends BFF sessions on top of the session store and the session codec.
/// </summary>
internal interface IBffSessionManager
{
    /// <summary>
    /// Creates a session under a new handle.
    /// </summary>
    ValueTask<BffSessionState> CreateAsync(
        BffSessionMetadata metadata,
        BffSessionPayload payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Loads and decrypts the session of a handle.
    /// </summary>
    ValueTask<BffSessionState?> LoadAsync(string handle, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a change to a session with an optimistic read-modify-write.
    /// </summary>
    ValueTask<BffSessionState?> UpdateAsync(
        BffSessionState current,
        Func<BffSessionState, BffSessionState?> update,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Ends a session and records why.
    /// </summary>
    ValueTask EndAsync(
        HttpContext? httpContext,
        string storeKey,
        BffSessionMetadata metadata,
        string reason,
        CancellationToken cancellationToken
    );
}
