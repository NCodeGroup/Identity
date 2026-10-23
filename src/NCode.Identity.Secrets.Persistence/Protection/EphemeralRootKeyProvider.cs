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
using NCode.Buffers;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides an implementation of <see cref="IRootKeyProvider"/> that generates a random root key when it is created
/// and keeps the key in memory for the lifetime of the object. A key that this provider wrapped can only be unwrapped
/// by the same instance, so it is suitable for development and tests but not for data that must survive a restart.
/// </summary>
/// <remarks>
/// The root key lives in a pinned buffer from the secure memory pool so that the garbage collector never copies it,
/// and it is zeroed when the provider is disposed.
/// </remarks>
// See ADR-0228.
internal class EphemeralRootKeyProvider : IRootKeyProvider, IDisposable
{
    private const int RootKeySizeBytes = 32;

    /// <summary>
    /// Initializes a new instance of the <see cref="EphemeralRootKeyProvider"/> class with a new random root key.
    /// </summary>
    public EphemeralRootKeyProvider()
    {
        RootKeyLease = BufferFactory.Rent(
            RootKeySizeBytes,
            isSensitive: true,
            out Memory<byte> rootKey
        );
        RandomNumberGenerator.Fill(rootKey.Span);
        RootKey = rootKey;

        KeyId = string.Create(
            AesGcmRootKeyWrapper.FingerprintLength,
            RootKey,
            static (chars, key) => AesGcmRootKeyWrapper.ComputeFingerprint(key.Span, chars)
        );
    }

    private IDisposable RootKeyLease { get; }

    private ReadOnlyMemory<byte> RootKey { get; }

    private string KeyId { get; }

    /// <inheritdoc />
    public string ProviderName => RootKeyProviderNames.Ephemeral;

    /// <inheritdoc />
    public ValueTask<RootWrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        var ciphertext = string.Create(
            AesGcmRootKeyWrapper.GetWrappedLength(key.Length),
            (RootKey: RootKey, Key: key, Context: context),
            static (chars, state) =>
            {
                var writer = chars.GetFixedBufferWriter();
                AesGcmRootKeyWrapper.Wrap(
                    state.RootKey.Span,
                    state.Key.Span,
                    state.Context.Span,
                    ref writer
                );
            }
        );

        var wrappedKey = new RootWrappedKey
        {
            ProviderName = ProviderName,
            KeyId = KeyId,
            Ciphertext = ciphertext,
        };

        return ValueTask.FromResult(wrappedKey);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="wrappedKey"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the key was wrapped by a different instance of this provider, which includes an instance in another
    /// process, or when the key was altered or the context does not match.
    /// </exception>
    public ValueTask<int> UnwrapAsync(
        RootWrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        Memory<byte> destination,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(wrappedKey);

        if (!string.Equals(wrappedKey.KeyId, KeyId, StringComparison.Ordinal))
        {
            throw new CryptographicException(
                "The key was wrapped by another process; an ephemeral root key exists only in memory."
            );
        }

        var written = AesGcmRootKeyWrapper.Unwrap(
            RootKey.Span,
            wrappedKey.Ciphertext,
            context.Span,
            destination.Span
        );
        return ValueTask.FromResult(written);
    }

    /// <summary>
    /// Zeroes the root key and returns it to the secure memory pool.
    /// </summary>
    public void Dispose()
    {
        RootKeyLease.Dispose();
    }
}
