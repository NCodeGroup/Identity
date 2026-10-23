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

using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Secrets.Persistence.Options;
using LoadedKey = (System.IDisposable Lease, System.ReadOnlyMemory<byte> Key);
using LoadedKeys = (
    System.ReadOnlyMemory<byte> CurrentKey,
    string CurrentKeyId,
    System.Collections.Generic.Dictionary<
        string,
        (System.IDisposable Lease, System.ReadOnlyMemory<byte> Key)
    > KeysById
);

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides an implementation of <see cref="IRootKeyProvider"/> that reads its root keys from the configuration or
/// from local files, such as a secret that the hosting environment mounts into the process.
/// </summary>
/// <remarks>
/// <para>
/// The keys are loaded once, on the first call to <see cref="WrapAsync"/> or <see cref="UnwrapAsync"/>, so a provider
/// that is not selected and is not configured never fails. A failure to load the keys is cached until the process
/// restarts, because a configuration error is fixed by deploying the corrected configuration.
/// </para>
/// <para>
/// Keys wrap with the current key. Keys that an older root key wrapped unwrap when the file of that root key is listed
/// in the previous key paths. Each key lives in a pinned buffer from the secure memory pool so that the garbage
/// collector never copies it, and the keys are zeroed when the provider is disposed. A key file is read as bytes and a
/// key is never held as text, except for the inline key, which the configuration supplies as a <see cref="string"/>.
/// </para>
/// </remarks>
// See ADR-0228.
internal class LocalRootKeyProvider(IOptions<LocalRootKeyOptions> options)
    : IRootKeyProvider,
        IDisposable
{
    private const string SectionName = LocalRootKeyOptions.DefaultSectionName;

    private const int RootKeySizeBytes = 32;

    // A base64url encoded 32-byte key is 43 characters, so this leaves room for surrounding whitespace.
    private const int MaxKeyFileSizeBytes = 1024;

    // Room for the decoded bytes of a key that is longer than a root key, so that its length can be reported.
    private const int DecodeBufferSizeBytes = 64;

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private static ReadOnlySpan<byte> Utf16LittleEndianBom => [0xFF, 0xFE];

    private static ReadOnlySpan<byte> Utf16BigEndianBom => [0xFE, 0xFF];

    private Lazy<LoadedKeys> Keys { get; } =
        new(() => LoadKeys(options.Value), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <inheritdoc />
    public string ProviderName => RootKeyProviderNames.Local;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when the root keys are not configured correctly or cannot be read.
    /// </exception>
    public ValueTask<RootWrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        var (currentKey, currentKeyId, _) = Keys.Value;

        var ciphertext = string.Create(
            AesGcmRootKeyWrapper.GetWrappedLength(key.Length),
            (RootKey: currentKey, Key: key, Context: context),
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
            KeyId = currentKeyId,
            Ciphertext = ciphertext,
        };

        return ValueTask.FromResult(wrappedKey);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="wrappedKey"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the root keys are not configured correctly or cannot be read.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when none of the configured root keys wrapped the key, or when the key was altered or the context does
    /// not match.
    /// </exception>
    public ValueTask<int> UnwrapAsync(
        RootWrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        Memory<byte> destination,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(wrappedKey);

        var (_, _, keysById) = Keys.Value;

        if (!keysById.TryGetValue(wrappedKey.KeyId, out var rootKey))
        {
            throw new CryptographicException(
                "The key was wrapped by a root key that is not configured. "
                    + $"Add the file of the old root key to '{SectionName}:{nameof(LocalRootKeyOptions.PreviousKeyPaths)}'."
            );
        }

        var written = AesGcmRootKeyWrapper.Unwrap(
            rootKey.Key.Span,
            wrappedKey.Ciphertext,
            context.Span,
            destination.Span
        );
        return ValueTask.FromResult(written);
    }

    /// <summary>
    /// Zeroes the root keys and returns them to the secure memory pool, when they have been loaded.
    /// </summary>
    public void Dispose()
    {
        if (!Keys.IsValueCreated)
        {
            return;
        }

        foreach (var rootKey in Keys.Value.KeysById.Values)
        {
            rootKey.Lease.Dispose();
        }
    }

    private static LoadedKeys LoadKeys(LocalRootKeyOptions options)
    {
        var keysById = new Dictionary<string, LoadedKey>(StringComparer.Ordinal);
        var succeeded = false;
        try
        {
            var currentKey = LoadCurrentKey(options);
            var currentKeyId = ComputeFingerprint(currentKey.Key);
            keysById.Add(currentKeyId, currentKey);

            foreach (var path in options.PreviousKeyPaths)
            {
                var previousKey = ReadKeyFile(path, nameof(LocalRootKeyOptions.PreviousKeyPaths));
                var previousKeyId = ComputeFingerprint(previousKey.Key);
                if (!keysById.TryAdd(previousKeyId, previousKey))
                {
                    previousKey.Lease.Dispose();
                }
            }

            succeeded = true;
            return (currentKey.Key, currentKeyId, keysById);
        }
        finally
        {
            if (!succeeded)
            {
                foreach (var rootKey in keysById.Values)
                {
                    rootKey.Lease.Dispose();
                }
            }
        }
    }

    private static string ComputeFingerprint(ReadOnlyMemory<byte> rootKey) =>
        string.Create(
            AesGcmRootKeyWrapper.FingerprintLength,
            rootKey,
            static (chars, key) => AesGcmRootKeyWrapper.ComputeFingerprint(key.Span, chars)
        );

    private static LoadedKey LoadCurrentKey(LocalRootKeyOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Key) && string.IsNullOrWhiteSpace(options.KeyPath))
        {
            return DecodeKey(
                options.Key,
                $"The '{SectionName}:{nameof(LocalRootKeyOptions.Key)}' setting"
            );
        }

        if (string.IsNullOrWhiteSpace(options.Key) && !string.IsNullOrWhiteSpace(options.KeyPath))
        {
            return ReadKeyFile(options.KeyPath, nameof(LocalRootKeyOptions.KeyPath));
        }

        throw new InvalidOperationException(
            $"Exactly one of '{SectionName}:{nameof(LocalRootKeyOptions.Key)}' and "
                + $"'{SectionName}:{nameof(LocalRootKeyOptions.KeyPath)}' must be set to use the local root key provider."
        );
    }

    private static LoadedKey ReadKeyFile(string path, string settingName)
    {
        var source = $"The root key file '{path}' that '{SectionName}:{settingName}' names";
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read);

            var length = RandomAccess.GetLength(handle);
            if (length > MaxKeyFileSizeBytes)
            {
                throw new InvalidOperationException(
                    $"{source} must not be longer than {MaxKeyFileSizeBytes} bytes."
                );
            }

            using var lease = BufferFactory.Rent(
                (int)length,
                isSensitive: true,
                out Span<byte> utf8
            );

            var bytesRead = RandomAccess.Read(handle, utf8, 0);
            var content = utf8[..bytesRead];

            if (content.StartsWith(Utf16LittleEndianBom) || content.StartsWith(Utf16BigEndianBom))
            {
                throw new InvalidOperationException(
                    $"{source} must be UTF-8 or ASCII base64url text, not UTF-16."
                );
            }

            if (content.StartsWith(Utf8Bom))
            {
                content = content[Utf8Bom.Length..];
            }

            var encoded = content.Trim(" \t\r\n"u8);

            return DecodeUtf8Key(encoded, source);
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or NotSupportedException
                        or ArgumentException
            )
        {
            throw new InvalidOperationException(
                $"The root key file '{path}' that '{SectionName}:{settingName}' names cannot be read.",
                exception
            );
        }
    }

    private static LoadedKey DecodeUtf8Key(ReadOnlySpan<byte> encoded, string source)
    {
        Span<byte> decoded = stackalloc byte[DecodeBufferSizeBytes];
        using var lifetime = decoded.GetSecureLifetime();

        var status = System.Buffers.Text.Base64Url.DecodeFromUtf8(
            encoded,
            decoded,
            out _,
            out var bytesWritten
        );

        return status switch
        {
            OperationStatus.Done => CreateKey(decoded[..bytesWritten], source),
            OperationStatus.InvalidData => throw InvalidBase64Url(source),
            _ => throw InvalidKeyLength(source),
        };
    }

    private static LoadedKey DecodeKey(string encoded, string source)
    {
        Span<byte> decoded = stackalloc byte[DecodeBufferSizeBytes];
        using var lifetime = decoded.GetSecureLifetime();

        int bytesWritten;
        try
        {
            if (!Base64Url.TryDecode(encoded, decoded, out bytesWritten))
            {
                throw InvalidKeyLength(source);
            }
        }
        catch (FormatException)
        {
            // The format error is not chained because its message might quote a part of the key.
            throw InvalidBase64Url(source);
        }

        return CreateKey(decoded[..bytesWritten], source);
    }

    private static LoadedKey CreateKey(ReadOnlySpan<byte> decoded, string source)
    {
        if (decoded.Length != RootKeySizeBytes)
        {
            throw InvalidKeyLength(source);
        }

        var lease = BufferFactory.Rent(RootKeySizeBytes, isSensitive: true, out Memory<byte> key);
        decoded.CopyTo(key.Span);
        return (lease, key);
    }

    private static InvalidOperationException InvalidBase64Url(string source) =>
        new($"{source} is not valid base64url text.");

    private static InvalidOperationException InvalidKeyLength(string source) =>
        new($"{source} must decode to exactly {RootKeySizeBytes} bytes.");
}
