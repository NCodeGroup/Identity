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
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the default implementation of <see cref="IRecoveryKeyWrapper"/>, which wraps a master key under an RSA
/// public key with RSA-OAEP and SHA-256.
/// </summary>
/// <remarks>
/// <para>
/// The public key is read once, when the wrapper is created, from the PEM file that the recovery options name. The
/// key must be an RSA public key in the <c>PUBLIC KEY</c> (SubjectPublicKeyInfo) format whose modulus is at least
/// 3072 bits. A PEM that holds any other kind of key, including a private key, is refused so that the private half
/// of the recovery key is never deployed.
/// </para>
/// <para>
/// When the options disable recovery or do not configure a key, no recovery key is loaded and nothing is wrapped.
/// </para>
/// </remarks>
// See ADR-0235.
internal class RecoveryKeyWrapper : IRecoveryKeyWrapper, IDisposable
{
    /// <summary>
    /// The name that <see cref="Name"/> reports. The name is never persisted or configured, because the recovery
    /// wrapper is not a root key wrapper.
    /// </summary>
    public const string WrapperName = "recovery-rsa-oaep-256";

    private const string SectionName =
        $"{SecretProtectionOptions.DefaultSectionName}:{nameof(SecretProtectionOptions.Recovery)}";

    private const string PublicKeyLabel = "PUBLIC KEY";

    private const int MinimumModulusBits = 3072;

    // The DER overhead of a SubjectPublicKeyInfo that holds an RSA public key is below 64 bytes, and the public
    // exponent of a valid key is shorter than its modulus.
    private const int SubjectPublicKeyInfoOverheadBytes = 64;

    private RSA? PublicKey { get; }

    // The size of an RSA-OAEP ciphertext, which equals the size of the modulus.
    private int WrappedSizeBytes { get; }

    private int WrappedLength { get; }

    /// <summary>
    /// Gets the identifier of the recovery public key, which is the base64url-encoded SHA-256 digest of the
    /// SubjectPublicKeyInfo of the key, or <see langword="null"/> when no recovery key is configured.
    /// </summary>
    public string? RecoveryKeyId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecoveryKeyWrapper"/> class by loading the recovery public key
    /// that the options configure, if any.
    /// </summary>
    /// <param name="options">The options that configure the recovery key.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the options both disable recovery and name a public key, or when the configured public key cannot
    /// be read or is not an RSA public key of at least 3072 bits.
    /// </exception>
    public RecoveryKeyWrapper(IOptions<SecretProtectionOptions> options)
        : this(LoadPublicKey(options.Value.Recovery)) { }

    private RecoveryKeyWrapper(RSA? publicKey)
    {
        PublicKey = publicKey;

        if (publicKey is not null)
        {
            RecoveryKeyId = ComputeRecoveryKeyId(publicKey);
            WrappedSizeBytes = (publicKey.KeySize + 7) / 8;
            WrappedLength = Base64Url.GetCharCountForEncode(WrappedSizeBytes);
        }
    }

    /// <summary>
    /// Creates a wrapper over the RSA public key in the specified PEM text.
    /// </summary>
    /// <param name="pem">The PEM text of an RSA public key with the <c>PUBLIC KEY</c> label.</param>
    /// <returns>The new wrapper.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the text is not an RSA public key of at least 3072 bits.
    /// </exception>
    internal static RecoveryKeyWrapper FromPem(string pem) => new(ImportPublicKey(pem));

    /// <inheritdoc />
    public string Name => WrapperName;

    /// <inheritdoc />
    public bool IsConfigured => PublicKey is not null;

    /// <inheritdoc />
    /// <remarks>
    /// RSA-OAEP takes no associated data here, so the context is not bound; the key-check value of the master key
    /// binds it to its owner and version.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no recovery key is configured, or when the wrapper writes fewer characters than it reported.
    /// </exception>
    public ValueTask<WrappedKey> WrapAsync(
        ReadOnlyMemory<byte> key,
        ReadOnlyMemory<byte> context,
        CancellationToken cancellationToken
    )
    {
        if (PublicKey is null || RecoveryKeyId is not { } keyId)
        {
            throw new InvalidOperationException("No recovery key is configured.");
        }

        var ciphertext = string.Create(
            WrappedLength,
            (Wrapper: this, Key: key),
            static (chars, state) =>
            {
                var writer = chars.GetFixedBufferWriter();
                state.Wrapper.Wrap(state.Key.Span, ref writer);

                if (writer.WrittenCount != chars.Length)
                {
                    throw new InvalidOperationException(
                        "The recovery key wrapper wrote fewer characters than it reported."
                    );
                }
            }
        );

        return ValueTask.FromResult(
            new WrappedKey
            {
                WrapperName = Name,
                KeyId = keyId,
                Ciphertext = ciphertext,
            }
        );
    }

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">
    /// Always thrown, because the recovery private key is kept offline.
    /// </exception>
    public ValueTask UnwrapAsync(
        WrappedKey wrappedKey,
        ReadOnlyMemory<byte> context,
        IBufferWriter<byte> destination,
        CancellationToken cancellationToken
    ) =>
        throw new NotSupportedException(
            "The recovery private key is kept offline, so the recovery key wrapper can only wrap."
        );

    /// <summary>
    /// Gets the number of characters that <see cref="Wrap{TWriter}"/> writes.
    /// </summary>
    /// <returns>
    /// The length of the base64url-encoded wrapped key, or zero when no recovery key is configured.
    /// </returns>
    public int GetWrappedLength() => WrappedLength;

    /// <summary>
    /// Wraps a master key under the recovery public key with RSA-OAEP and SHA-256, and writes the base64url text of
    /// the wrapped key to a writer.
    /// </summary>
    /// <typeparam name="TWriter">The type of the writer that receives the wrapped key.</typeparam>
    /// <param name="masterKey">The master key to wrap.</param>
    /// <param name="destination">
    /// The writer that receives exactly <see cref="GetWrappedLength"/> characters.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no recovery key is configured, or when <paramref name="destination"/> does not provide the
    /// requested space.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// Thrown when the master key cannot be wrapped under the recovery public key.
    /// </exception>
    public void Wrap<TWriter>(ReadOnlySpan<byte> masterKey, ref TWriter destination)
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        if (PublicKey is null)
        {
            throw new InvalidOperationException("No recovery key is configured.");
        }

        using var lease = BufferFactory.Rent(
            WrappedSizeBytes,
            isSensitive: false,
            out Span<byte> wrapped
        );

        if (
            !PublicKey.TryEncrypt(
                masterKey,
                wrapped,
                RSAEncryptionPadding.OaepSHA256,
                out var bytesWritten
            )
        )
        {
            throw new CryptographicException(
                "The master key could not be wrapped under the recovery key."
            );
        }

        var chars = destination.GetSpan(WrappedLength);
        if (!Base64Url.TryEncode(wrapped[..bytesWritten], chars, out var charsWritten))
        {
            throw new InvalidOperationException(
                "The destination did not provide the requested space."
            );
        }

        destination.Advance(charsWritten);
    }

    /// <summary>
    /// Disposes the recovery public key, when one is loaded.
    /// </summary>
    public void Dispose()
    {
        PublicKey?.Dispose();
    }

    private static RSA? LoadPublicKey(RecoveryKeyOptions recovery)
    {
        var path = recovery.PublicKeyPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (recovery.Disabled)
        {
            throw new InvalidOperationException(
                $"'{SectionName}:{nameof(RecoveryKeyOptions.PublicKeyPath)}' and "
                    + $"'{SectionName}:{nameof(RecoveryKeyOptions.Disabled)}' cannot both be set. "
                    + "Remove the public key path to run without a recovery key, or set "
                    + $"'{SectionName}:{nameof(RecoveryKeyOptions.Disabled)}' to false."
            );
        }

        string pem;
        try
        {
            pem = File.ReadAllText(path, SecureEncoding.UTF8);
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
                $"The recovery public key file '{path}' that "
                    + $"'{SectionName}:{nameof(RecoveryKeyOptions.PublicKeyPath)}' names cannot be read.",
                exception
            );
        }

        return ImportPublicKey(pem);
    }

    private static string ComputeRecoveryKeyId(RSA publicKey)
    {
        var capacity = ((publicKey.KeySize + 7) / 8 * 2) + SubjectPublicKeyInfoOverheadBytes;
        using var lease = BufferFactory.Rent(capacity, isSensitive: false, out Span<byte> buffer);
        if (!publicKey.TryExportSubjectPublicKeyInfo(buffer, out var bytesWritten))
        {
            throw new InvalidOperationException(
                "The recovery public key could not be exported to compute its identifier."
            );
        }

        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(buffer[..bytesWritten], hash);

        // The identifier is persisted with each wrapped master key, which makes it a storage boundary.
        return Base64Url.Encode(hash);
    }

    private static RSA ImportPublicKey(string pem)
    {
        if (
            !PemEncoding.TryFind(pem, out var fields)
            || !pem.AsSpan(fields.Label).SequenceEqual(PublicKeyLabel)
        )
        {
            throw new InvalidOperationException(
                $"The recovery key must be an RSA public key in PEM format with the '{PublicKeyLabel}' label; "
                    + "never deploy the private key."
            );
        }

        var base64 = pem.AsSpan()[fields.Base64Data];
        var capacity = (base64.Length / 4 * 3) + 3;
        using var lease = BufferFactory.Rent(capacity, isSensitive: false, out Span<byte> buffer);
        if (!Convert.TryFromBase64Chars(base64, buffer, out var bytesWritten))
        {
            throw new InvalidOperationException(
                $"The recovery key must be an RSA public key in PEM format with the '{PublicKeyLabel}' label; "
                    + "its Base64 data is not valid."
            );
        }

        var subjectPublicKeyInfo = buffer[..bytesWritten];

        var rsa = RSA.Create();
        var succeeded = false;
        try
        {
            rsa.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);

            if (rsa.KeySize < MinimumModulusBits)
            {
                throw new InvalidOperationException(
                    $"The recovery key must be at least {MinimumModulusBits} bits but it is {rsa.KeySize} bits."
                );
            }

            succeeded = true;
            return rsa;
        }
        finally
        {
            if (!succeeded)
            {
                rsa.Dispose();
            }
        }
    }
}
