#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NCode.Encoders;
using NCode.Jose.Buffers;
using NCode.Jose.Exceptions;
using NCode.Jose.Extensions;
using NCode.Jose.Json;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Algorithms.KeyManagement;

/// <summary>
/// Provides an implementation of <see cref="KeyManagementAlgorithm"/> that uses the <c>PBKDF2 with AES</c> cryptographic algorithm for key management.
/// </summary>
public class Pbes2KeyManagementAlgorithm : CommonKeyManagementAlgorithm
{
    internal const int SaltInputSizeBytes = 12;
    internal const int MinIterationCount = 1000;
    private const int DefaultIterationCount = 8192;

    private static IEnumerable<KeySizes> StaticKeyBitSizes { get; } = new[]
    {
        new KeySizes(minSize: 8, maxSize: int.MaxValue, skipSize: 8)
    };

    private IAesKeyWrap AesKeyWrap { get; }

    /// <inheritdoc />
    public override string Code { get; }

    /// <inheritdoc />
    public override Type KeyType => typeof(SymmetricSecretKey);

    /// <inheritdoc />
    public override IEnumerable<KeySizes> KeyBitSizes => StaticKeyBitSizes;

    private HashAlgorithmName HashAlgorithmName { get; }

    internal int KeySizeBytes { get; }

    private int MaxIterationCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pbes2KeyManagementAlgorithm"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Provides the AES key wrap functionality.</param>
    /// <param name="code">Contains a <see cref="string"/> value that uniquely identifies the cryptographic algorithm.</param>
    /// <param name="hashAlgorithmName">Contains a <see cref="HashAlgorithmName"/> value that specifies the type of hash function to use.</param>
    /// <param name="keySizeBits">Contains the size, in bits, of the derived key encryption key (KEK).</param>
    /// <param name="maxIterationCount">Contains the maximum number of iterations allowed for the PBKDF2 algorithm.</param>
    public Pbes2KeyManagementAlgorithm(
        IAesKeyWrap aesKeyWrap,
        string code,
        HashAlgorithmName hashAlgorithmName,
        int keySizeBits,
        int maxIterationCount)
    {
        AesKeyWrap = aesKeyWrap;
        Code = code;
        HashAlgorithmName = hashAlgorithmName;
        KeySizeBytes = (keySizeBits + 7) >> 3;
        MaxIterationCount = maxIterationCount;
    }

    /// <inheritdoc />
    public override IEnumerable<KeySizes> GetLegalCekByteSizes(int kekSizeBits) =>
        AesKeyWrap.LegalCekByteSizes;

    /// <inheritdoc />
    public override int GetEncryptedContentKeySizeBytes(int kekSizeBits, int cekSizeBytes) =>
        AesKeyWrap.GetEncryptedContentKeySizeBytes(cekSizeBytes);

    /// <inheritdoc />
    public override bool TryWrapKey(
        SecretKey secretKey,
        IDictionary<string, object> header,
        ReadOnlySpan<byte> contentKey,
        Span<byte> encryptedContentKey,
        out int bytesWritten)
    {
        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(KeyBitSizes);

        if (!header.TryGetValue<string>(JoseClaimNames.Header.Alg, out var alg))
        {
            throw new JoseException("The JWT header is missing the 'alg' field.");
        }

        if (!header.TryGetValue<int>(JoseClaimNames.Header.P2c, out var iterationCount))
        {
            iterationCount = DefaultIterationCount;
        }

        if (iterationCount < MinIterationCount)
        {
            throw new JoseException($"The 'p2c' field in the JWT header must be at least {MinIterationCount}");
        }

        if (iterationCount > MaxIterationCount)
        {
            throw new JoseException($"The 'p2c' field in the JWT header must be at most {MaxIterationCount}");
        }

        ValidateContentKeySize(secretKey.KeySizeBits, contentKey.Length);

        var minEncryptedContentKey = AesKeyWrap.GetEncryptedContentKeySizeBytes(contentKey.Length);
        if (encryptedContentKey.Length < minEncryptedContentKey)
        {
            bytesWritten = 0;
            return false;
        }

        var algByteCount = Encoding.UTF8.GetByteCount(alg);
        var saltByteCount = algByteCount + 1 + SaltInputSizeBytes;

        var salt = saltByteCount <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[saltByteCount] :
            GC.AllocateUninitializedArray<byte>(saltByteCount, pinned: false);

        var algBytesWritten = Encoding.UTF8.GetBytes(alg, salt);
        Debug.Assert(algBytesWritten == algByteCount);

        salt[algByteCount] = 0x00;

        var saltInput = salt[(algByteCount + 1)..];
        Debug.Assert(saltInput.Length == SaltInputSizeBytes);

        RandomNumberGenerator.Fill(saltInput);

        header[JoseClaimNames.Header.P2c] = iterationCount;
        header[JoseClaimNames.Header.P2s] = Base64Url.Encode(saltInput);

        var newKek = KeySizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[KeySizeBytes] :
            GC.AllocateUninitializedArray<byte>(KeySizeBytes, pinned: true);

        using var _ = CryptoPool.Rent(
            validatedSecretKey.KeySizeBytes,
            isSensitive: true,
            out Span<byte> encryptionKey);

        var exportResult = validatedSecretKey.TryExportPrivateKey(encryptionKey, out var exportBytesWritten);
        Debug.Assert(exportResult && exportBytesWritten == validatedSecretKey.KeySizeBytes);

        try
        {
            Rfc2898DeriveBytes.Pbkdf2(
                encryptionKey,
                salt,
                newKek,
                iterationCount,
                HashAlgorithmName);

            return AesKeyWrap.TryWrapKey(newKek, contentKey, encryptedContentKey, out bytesWritten);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKek);
        }
    }

    /// <inheritdoc />
    public override bool TryUnwrapKey(
        SecretKey secretKey,
        JsonElement header,
        ReadOnlySpan<byte> encryptedContentKey,
        Span<byte> contentKey,
        out int bytesWritten)
    {
        // TODO: unit test for the edge cases in this method

        var validatedSecretKey = secretKey.Validate<SymmetricSecretKey>(KeyBitSizes);

        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.Alg, out var alg))
        {
            throw new JoseException("The JWT header is missing the 'alg' field.");
        }

        if (!header.TryGetPropertyValue<int>(JoseClaimNames.Header.P2c, out var iterationCount))
        {
            throw new JoseException("The JWT header is missing the 'p2c' field.");
        }

        if (!header.TryGetPropertyValue<string>(JoseClaimNames.Header.P2s, out var saltInputString))
        {
            throw new JoseException("The JWT header is missing the 'p2s' field.");
        }

        if (iterationCount < MinIterationCount)
        {
            throw new JoseException($"The 'p2c' field in the JWT header must be at least {MinIterationCount}");
        }

        if (iterationCount > MaxIterationCount)
        {
            throw new JoseException($"The 'p2c' field in the JWT header must be at most {MaxIterationCount}");
        }

        var saltInputByteCount = Base64Url.GetByteCountForDecode(saltInputString.Length);
        if (saltInputByteCount != SaltInputSizeBytes)
        {
            throw new JoseException("The salt input ('p2s') does not have a valid size for this cryptographic algorithm.");
        }

        const int minEncryptedContentKeyByteCount = KeyManagement.AesKeyWrap.IntermediateByteCount + KeyManagement.AesKeyWrap.ChunkByteCount;
        if (encryptedContentKey.Length < minEncryptedContentKeyByteCount || encryptedContentKey.Length % KeyManagement.AesKeyWrap.ChunkByteCount != 0)
        {
            throw new JoseException("The encrypted content encryption key (CEK) does not have a valid size for this cryptographic algorithm.");
        }

        var minContentKeyByteCount = encryptedContentKey.Length - KeyManagement.AesKeyWrap.ChunkByteCount;
        if (contentKey.Length < minContentKeyByteCount)
        {
            bytesWritten = 0;
            return false;
        }

        var algByteCount = Encoding.UTF8.GetByteCount(alg);
        var saltByteCount = algByteCount + 1 + SaltInputSizeBytes;

        var salt = saltByteCount <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[saltByteCount] :
            GC.AllocateUninitializedArray<byte>(saltByteCount, pinned: false);

        var algBytesWritten = Encoding.UTF8.GetBytes(alg, salt);
        Debug.Assert(algBytesWritten == algByteCount);

        salt[algByteCount] = 0x00;

        var saltInput = salt[(algByteCount + 1)..];
        Debug.Assert(saltInput.Length == SaltInputSizeBytes);
        var saltInputResult = Base64Url.TryDecode(saltInputString, saltInput, out var saltInputBytesWritten);
        Debug.Assert(saltInputResult && saltInputBytesWritten == SaltInputSizeBytes);

        var newKek = KeySizeBytes <= JoseConstants.MaxStackAlloc ?
            stackalloc byte[KeySizeBytes] :
            GC.AllocateUninitializedArray<byte>(KeySizeBytes, pinned: true);

        using var _ = CryptoPool.Rent(
            validatedSecretKey.KeySizeBytes,
            isSensitive: true,
            out Span<byte> encryptionKey);

        var exportResult = validatedSecretKey.TryExportPrivateKey(encryptionKey, out var exportBytesWritten);
        Debug.Assert(exportResult && exportBytesWritten == validatedSecretKey.KeySizeBytes);

        try
        {
            Rfc2898DeriveBytes.Pbkdf2(
                encryptionKey,
                salt,
                newKek,
                iterationCount,
                HashAlgorithmName);

            return AesKeyWrap.TryUnwrapKey(newKek, encryptedContentKey, contentKey, out bytesWritten);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(newKek);
        }
    }
}
