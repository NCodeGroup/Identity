// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses <c>RSAES-OAEP</c> for key management:
/// <c>RSA-OAEP</c>, <c>RSA-OAEP-256</c>, <c>RSA-OAEP-384</c> and <c>RSA-OAEP-512</c>,
/// with a local key, or through the key management service that holds a remote key.
/// </summary>
/// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
/// <param name="hashAlgorithmName">The hash function that <c>OAEP</c> and its <c>MGF1</c> mask generation function use.</param>
// See ADR-1477.
internal class RsaOaepKeyManagementAlgorithm(string code, HashAlgorithmName hashAlgorithmName)
    : CommonKeyManagementAlgorithm
{
    private static IEnumerable<KeySizes> StaticKeyBitSizes { get; } =
    [new(minSize: 2048, maxSize: 16384, skipSize: 64)];

    private static IReadOnlyList<AlgorithmKeyType> StaticKeyTypes { get; } =
    [new(typeof(RsaSecretKey), StaticKeyBitSizes)];

    /// <inheritdoc />
    public override string Code { get; } = code;

    /// <inheritdoc />
    public override IReadOnlyList<AlgorithmKeyType> KeyTypes => StaticKeyTypes;

    private RSAEncryptionPadding Padding { get; } =
        RSAEncryptionPadding.CreateOaep(hashAlgorithmName);

    private int GetMaxCekSizeBytes(int kekSizeBits)
    {
        // https://crypto.stackexchange.com/a/42100
        // ceil(kLenBits/8) - 2*ceil(hLenBits/8) - 2
        var hLenBits = Padding.OaepHashAlgorithm.GetHashSizeBits();
        var maxSizeBytes = ((kekSizeBits + 7) >> 3) - (((hLenBits + 7) >> 3) << 1) - 2;
        return maxSizeBytes;
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits)
    {
        const int octetSize = 1;
        var maxCekSizeBytes = GetMaxCekSizeBytes(kekSizeBits);
        return [new KeySizes(minSize: octetSize, maxCekSizeBytes, skipSize: octetSize)];
    }

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        (kekSizeBits + 7) >> 3;

    /// <inheritdoc />
    public override void WrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        IBufferWriter<byte> encryptedContentKeyWriter
    )
    {
        var validatedSecretKey = secretKey.Validate<RsaSecretKey>(StaticKeyBitSizes);

        using var key = validatedSecretKey.ExportRSA();

        var encryptedContentKey = encryptedContentKeyWriter.GetSpan(secretKey.KeySizeBytes);

        var bytesWritten = key.Encrypt(contentKey, encryptedContentKey, Padding);

        encryptedContentKeyWriter.Advance(bytesWritten);
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten
    )
    {
        if (secretKey is IRemoteSecretKey)
            throw new JoseInvalidAlgorithmException(
                "A key that a key management service holds unwraps a content key only through UnwrapKeyAsync."
            );

        var validatedSecretKey = secretKey.Validate<RsaSecretKey>(StaticKeyBitSizes);

        using var key = validatedSecretKey.ExportRSA();

        return key.TryDecrypt(encryptedContentKey, contentKey, Padding, out bytesWritten);
    }

    /// <inheritdoc />
    public override ValueTask<int> UnwrapKeyAsync(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlyMemory<byte> encryptedContentKey,
        Memory<byte> contentKey,
        CancellationToken cancellationToken
    )
    {
        if (secretKey is not IRemoteSecretKey remoteKey)
            return base.UnwrapKeyAsync(
                secretKey,
                header,
                encryptedContentKey,
                contentKey,
                cancellationToken
            );

        secretKey.Validate<RsaSecretKey>(StaticKeyBitSizes);

        // the local RSA.TryDecrypt refuses such a key the same way, so a key management service never receives one
        if (encryptedContentKey.Length != secretKey.KeySizeBytes)
            throw new CryptographicException(
                "The encrypted content encryption key (CEK) is not the size of the key's modulus."
            );

        return remoteKey.DecryptAsync(Code, encryptedContentKey, contentKey, cancellationToken);
    }
}
