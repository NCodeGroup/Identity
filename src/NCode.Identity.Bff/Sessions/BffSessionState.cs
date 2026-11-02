// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// A decrypted BFF session together with the handle and version it was read at.
/// </summary>
/// <param name="Handle">The secret session handle that the browser carries.</param>
/// <param name="StoreKey">The key of the session record in the store.</param>
/// <param name="Version">The version of the session record.</param>
/// <param name="Metadata">The plaintext metadata of the session.</param>
/// <param name="Payload">The decrypted payload of the session.</param>
internal sealed record BffSessionState(
    string Handle,
    string StoreKey,
    long Version,
    BffSessionMetadata Metadata,
    BffSessionPayload Payload
);
