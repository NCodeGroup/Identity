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

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// An opaque session record as an <c>IBffSessionStore</c> holds it: plaintext metadata and an encrypted payload.
/// </summary>
/// <param name="storeKey">The key of the record in the store.</param>
/// <param name="version">The version of the record; the store bumps it on every replace.</param>
/// <param name="metadata">The plaintext metadata.</param>
/// <param name="protectedPayload">The encrypted payload.</param>
[PublicAPI]
public sealed class BffSessionRecord(
    string storeKey,
    long version,
    BffSessionMetadata metadata,
    ReadOnlyMemory<byte> protectedPayload
)
{
    /// <summary>
    /// Gets the key of the record in the store.
    /// </summary>
    public string StoreKey { get; } = ValidateStoreKey(storeKey, nameof(storeKey));

    /// <summary>
    /// Gets the version of the record.
    /// </summary>
    public long Version { get; } = ValidateVersion(version, nameof(version));

    /// <summary>
    /// Gets the plaintext metadata.
    /// </summary>
    public BffSessionMetadata Metadata { get; } =
        metadata ?? throw new ArgumentNullException(nameof(metadata));

    /// <summary>
    /// Gets the encrypted payload.
    /// </summary>
    public ReadOnlyMemory<byte> ProtectedPayload { get; } = protectedPayload;

    private static string ValidateStoreKey(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }

    private static long ValidateVersion(long value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        return value;
    }
}
