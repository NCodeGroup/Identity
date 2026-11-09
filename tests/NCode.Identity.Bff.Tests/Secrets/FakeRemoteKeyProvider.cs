// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Bff.Tests.Secrets;

/// <summary>
/// A remote key provider that stands in for a key management service: it holds one ES256 signing key and one
/// RSA-OAEP-256 decryption key in memory, and counts every signature and decryption so a test can tell that the
/// service used a key. A test sets <see cref="ActualSigningKey"/> or <see cref="ActualDecryptionKey"/> to make the
/// service use another key than the one whose public half it returns.
/// </summary>
internal sealed class FakeRemoteKeyProvider
    : IRemoteKeyProvider,
        IRemoteKeyDescriptorSource,
        IDisposable
{
    /// <summary>
    /// The name that identifies this provider.
    /// </summary>
    public const string ProviderName = "test-kms";

    /// <summary>
    /// The identifier of the signing key in the service.
    /// </summary>
    public const string SigningKeyId = "kms-sig";

    /// <summary>
    /// The identifier of the decryption key in the service.
    /// </summary>
    public const string DecryptionKeyId = "kms-enc";

    private static IReadOnlyList<RemoteKeyAlgorithm> Rows { get; } =
    [
        new RemoteSigningAlgorithm
        {
            Provider = ProviderName,
            Algorithm = "ES256",
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = EccCurveSpecification.P256.CurveSizeBits,
            Curve = EccCurveSpecification.P256.CurveName,
            KeySpec = "ECC_NIST_P256",
            SigningAlgorithm = "ECDSA_SHA_256",
            DigestAlgorithm = HashAlgorithmName.SHA256,
            IsDerEncodedSignature = true,
        },
        new RemoteDecryptionAlgorithm
        {
            Provider = ProviderName,
            Algorithm = "RSA-OAEP-256",
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 2048,
            KeySpec = "RSA_2048",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        },
    ];

    private long _signatureCount;

    private long _decryptionCount;

    /// <summary>
    /// Gets the signing key that the service holds.
    /// </summary>
    public ECDsa SigningKey { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>
    /// Gets the decryption key that the service holds.
    /// </summary>
    public RSA DecryptionKey { get; } = RSA.Create(2048);

    /// <summary>
    /// Gets or sets the key that signs in place of <see cref="SigningKey"/> when set.
    /// </summary>
    public ECDsa? ActualSigningKey { get; set; }

    /// <summary>
    /// Gets or sets the key that decrypts in place of <see cref="DecryptionKey"/> when set.
    /// </summary>
    public RSA? ActualDecryptionKey { get; set; }

    /// <summary>
    /// Gets the number of signatures that the provider produced.
    /// </summary>
    public int SignatureCount => (int)Interlocked.Read(ref _signatureCount);

    /// <summary>
    /// Gets the number of ciphertexts that the provider decrypted.
    /// </summary>
    public int DecryptionCount => (int)Interlocked.Read(ref _decryptionCount);

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public IEnumerable<RemoteKeyProviderDescriptor> Providers { get; } =
    [new RemoteKeyProviderDescriptor { Name = ProviderName }];

    /// <inheritdoc />
    public IEnumerable<RemoteKeyAlgorithm> Algorithms => Rows;

    /// <inheritdoc />
    public ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask GetPublicKeyAsync(
        RemotePublicKeyRequest request,
        IBufferWriter<byte> subjectPublicKeyInfo,
        CancellationToken cancellationToken
    )
    {
        AsymmetricAlgorithm key = request.KeyId switch
        {
            SigningKeyId => SigningKey,
            DecryptionKeyId => DecryptionKey,
            _ => throw new CryptographicException($"The key '{request.KeyId}' does not exist."),
        };

        subjectPublicKeyInfo.Write(key.ExportSubjectPublicKeyInfo());
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<int> SignAsync(
        RemoteSignatureRequest request,
        Memory<byte> signature,
        CancellationToken cancellationToken
    )
    {
        var key = ActualSigningKey ?? SigningKey;
        if (
            !key.TrySignHash(
                request.Data.Span,
                signature.Span,
                DSASignatureFormat.Rfc3279DerSequence,
                out var bytesWritten
            )
        )
        {
            throw new CryptographicException("The signature does not fit in the buffer.");
        }

        Interlocked.Increment(ref _signatureCount);
        return ValueTask.FromResult(bytesWritten);
    }

    /// <inheritdoc />
    public ValueTask<int> DecryptAsync(
        RemoteDecryptionRequest request,
        Memory<byte> plaintext,
        CancellationToken cancellationToken
    )
    {
        var key = ActualDecryptionKey ?? DecryptionKey;
        if (
            !key.TryDecrypt(
                request.Ciphertext.Span,
                plaintext.Span,
                RSAEncryptionPadding.OaepSHA256,
                out var bytesWritten
            )
        )
        {
            throw new CryptographicException("The plaintext does not fit in the buffer.");
        }

        Interlocked.Increment(ref _decryptionCount);
        return ValueTask.FromResult(bytesWritten);
    }

    /// <inheritdoc />
    public ValueTask<int> DeriveSharedSecretAsync(
        RemoteKeyAgreementRequest request,
        Memory<byte> sharedSecret,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    /// <inheritdoc />
    public IAsyncEnumerable<RemoteKeyListing> ListKeysAsync(
        RemoteKeyListRequest request,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose()
    {
        SigningKey.Dispose();
        DecryptionKey.Dispose();
    }
}
