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

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the ability to wrap and unwrap a master key with a root key that is held outside of the database, for
/// example by a key management service or in a file that is mounted into the process. Each implementation is
/// identified by <see cref="ProviderName"/> so that a wrapped key records which provider can unwrap it.
/// </summary>
// See ADR-0228.
[PublicAPI]
public interface IRootKeyProvider
{
    /// <summary>
    /// Gets the name that identifies this provider, such as one of the values in <see cref="RootKeyProviderNames"/>.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Wraps a key with the root key of this provider.
    /// </summary>
    /// <param name="key">The key to wrap.</param>
    /// <param name="context">
    /// The associated data that the provider binds to the wrapped key when the root key supports it, so that the
    /// wrapped key can only be unwrapped with the same context.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The wrapped key together with the name of this provider and the identifier of the root key.</returns>
    ValueTask<RootWrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Unwraps a key that was wrapped by <see cref="WrapAsync"/> and writes it into a buffer that the caller provides.
    /// The caller owns the buffer, should rent it from a sensitive pool, and is responsible for zeroing it when the key
    /// is no longer needed.
    /// </summary>
    /// <param name="wrappedKey">The wrapped key to unwrap.</param>
    /// <param name="context">
    /// The associated data that the provider binds to the wrapped key when the root key supports it, which must
    /// match the context that was used to wrap the key.
    /// </param>
    /// <param name="destination">
    /// The buffer that receives the unwrapped key, which must be at least as long as the key. The provider writes
    /// nothing beyond the key, and leaves the bytes that it did write zeroed when the key cannot be unwrapped.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The number of bytes that were written to <paramref name="destination"/>, which is the length of the key.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="destination"/> is too small to hold the unwrapped key.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the key cannot be unwrapped, for example because it was altered, because the context does not match,
    /// or because the root key is not available.
    /// </exception>
    ValueTask<int> UnwrapAsync(
        RootWrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        Memory<byte> destination,
        CancellationToken cancellationToken
    );
}
