// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A remote key provider that stands in for a key management service: it holds each key it creates in memory, signs
/// the digest it receives the way a service does (RSASSA-PSS for <c>PS256</c>, PKCS #1 v1.5 for <c>RS256</c> and a DER
/// <c>SEQUENCE</c> for <c>ES256</c>), decrypts an RSA-OAEP ciphertext, agrees an ECDH shared secret, lists the keys it
/// holds by deployment, and counts every signature, decryption, agreement and destruction so a test can tell that the
/// service used or destroyed a key.
/// </summary>
public sealed class TestRemoteKeyProvider
    : IRemoteKeyProvider,
        IRemoteKeyDescriptorSource,
        IDisposable
{
    /// <summary>
    /// The name that identifies this provider.
    /// </summary>
    public const string ProviderName = "test-kms";

    private const string KeyIdPrefix = "test-kms-key-";

    private static IReadOnlyList<RemoteKeyAlgorithm> Rows { get; } =
    [
        CreateRsaRow("RS256", "RSASSA_PKCS1_V1_5_SHA_256"),
        CreateRsaRow("PS256", "RSASSA_PSS_SHA_256"),
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
            KeySizeBits = 3072,
            KeySpec = "RSA_3072",
            EncryptionAlgorithm = "RSAES_OAEP_SHA_256",
        },
        new RemoteKeyAgreementAlgorithm
        {
            Provider = ProviderName,
            Algorithm = "ECDH-ES",
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = EccCurveSpecification.P256.CurveSizeBits,
            Curve = EccCurveSpecification.P256.CurveName,
            KeySpec = "ECC_NIST_P256",
            KeyAgreementAlgorithm = "ECDH",
        },
    ];

    private ConcurrentDictionary<string, AsymmetricAlgorithm> Keys { get; } =
        new(StringComparer.Ordinal);

    private ConcurrentDictionary<
        string,
        (string Deployment, RemoteKeyListing Listing)
    > Listings { get; } = new(StringComparer.Ordinal);

    private ConcurrentQueue<string> CreatedKeys { get; } = new();

    private ConcurrentQueue<string> DestroyedKeys { get; } = new();

    private long _signatureCount;

    private long _decryptionCount;

    private long _agreementCount;

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public IEnumerable<RemoteKeyProviderDescriptor> Providers { get; } =
    [new RemoteKeyProviderDescriptor { Name = ProviderName }];

    /// <inheritdoc />
    public IEnumerable<RemoteKeyAlgorithm> Algorithms => Rows;

    /// <summary>
    /// Gets the number of signatures that the provider produced.
    /// </summary>
    public long SignatureCount => Interlocked.Read(ref _signatureCount);

    /// <summary>
    /// Gets the number of ciphertexts that the provider decrypted.
    /// </summary>
    public long DecryptionCount => Interlocked.Read(ref _decryptionCount);

    /// <summary>
    /// Gets the number of shared secrets that the provider agreed.
    /// </summary>
    public long AgreementCount => Interlocked.Read(ref _agreementCount);

    /// <summary>
    /// Gets the identifier of every key that the provider created, in order.
    /// </summary>
    public IReadOnlyList<string> CreatedKeyIds => [.. CreatedKeys];

    /// <summary>
    /// Gets the identifier of every key that the provider destroyed, in order.
    /// </summary>
    public IReadOnlyList<string> DestroyedKeyIds => [.. DestroyedKeys];

    /// <summary>
    /// Adds a key that the service already holds, one that the provider did not create, and returns its identifier.
    /// The provider disposes the key.
    /// </summary>
    /// <param name="key">The key that the service holds.</param>
    /// <returns>The identifier of the key in the service.</returns>
    public string AddKey(AsymmetricAlgorithm key)
    {
        var keyId = $"{KeyIdPrefix}{Guid.NewGuid():N}";
        Keys[keyId] = key;
        return keyId;
    }

    /// <inheritdoc />
    public ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        var algorithm = request.Algorithm;
        AsymmetricAlgorithm key;
        switch (algorithm.SecretType)
        {
            case SecretKeyTypes.Rsa:
                key = RSA.Create(algorithm.KeySizeBits);
                break;
            case SecretKeyTypes.Ecc:
                key =
                    algorithm is RemoteKeyAgreementAlgorithm
                        ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256)
                        : ECDsa.Create(ECCurve.NamedCurves.nistP256);
                break;
            default:
                return ValueTask.FromException<RemoteKeyCreationResult>(
                    new InvalidOperationException(
                        $"The provider holds no '{algorithm.SecretType}' key."
                    )
                );
        }

        var keyId = $"{KeyIdPrefix}{Guid.NewGuid():N}";
        Keys[keyId] = key;
        CreatedKeys.Enqueue(keyId);
        Listings[keyId] = (
            request.Deployment,
            new RemoteKeyListing
            {
                KeyId = keyId,
                GroupName = request.GroupName,
                CreatedWhen = DateTimeOffset.UtcNow,
                OwnerId = request.OwnerId,
                SecretId = request.SecretId,
            }
        );

        return ValueTask.FromResult(new RemoteKeyCreationResult { KeyId = keyId });
    }

    /// <inheritdoc />
    public ValueTask GetPublicKeyAsync(
        RemotePublicKeyRequest request,
        IBufferWriter<byte> subjectPublicKeyInfo,
        CancellationToken cancellationToken
    )
    {
        if (!Keys.TryGetValue(request.KeyId, out var key))
        {
            return ValueTask.FromException(
                new InvalidOperationException($"The key '{request.KeyId}' does not exist.")
            );
        }

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
        if (!Keys.TryGetValue(request.KeyId, out var key))
        {
            return ValueTask.FromException<int>(
                new InvalidOperationException($"The key '{request.KeyId}' does not exist.")
            );
        }

        var digest = request.Algorithm.DigestAlgorithm;
        if (digest is null)
        {
            return ValueTask.FromException<int>(
                new InvalidOperationException("The provider signs a digest.")
            );
        }

        int bytesWritten;
        bool isWritten;
        switch (key)
        {
            case RSA rsa:
                isWritten = rsa.TrySignHash(
                    request.Data.Span,
                    signature.Span,
                    digest.Value,
                    request.Algorithm.Algorithm.StartsWith("PS", StringComparison.Ordinal)
                        ? RSASignaturePadding.Pss
                        : RSASignaturePadding.Pkcs1,
                    out bytesWritten
                );
                break;
            case ECDsa ecdsa:
                isWritten = ecdsa.TrySignHash(
                    request.Data.Span,
                    signature.Span,
                    DSASignatureFormat.Rfc3279DerSequence,
                    out bytesWritten
                );
                break;
            default:
                return ValueTask.FromException<int>(
                    new InvalidOperationException($"The key '{request.KeyId}' cannot sign.")
                );
        }

        if (!isWritten)
        {
            return ValueTask.FromException<int>(
                new CryptographicException("The signature does not fit in the buffer.")
            );
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
        if (!Keys.TryGetValue(request.KeyId, out var key))
        {
            return ValueTask.FromException<int>(
                new InvalidOperationException($"The key '{request.KeyId}' does not exist.")
            );
        }

        if (key is not RSA rsa)
        {
            return ValueTask.FromException<int>(
                new InvalidOperationException($"The key '{request.KeyId}' cannot decrypt.")
            );
        }

        bool isWritten;
        int bytesWritten;
        try
        {
            isWritten = rsa.TryDecrypt(
                request.Ciphertext.Span,
                plaintext.Span,
                request.Algorithm.EncryptionAlgorithm == "RSAES_OAEP_SHA_256"
                    ? RSAEncryptionPadding.OaepSHA256
                    : RSAEncryptionPadding.OaepSHA1,
                out bytesWritten
            );
        }
        catch (CryptographicException exception)
        {
            return ValueTask.FromException<int>(
                new CryptographicException("The ciphertext does not decrypt.", exception)
            );
        }

        if (!isWritten)
        {
            return ValueTask.FromException<int>(
                new CryptographicException("The plaintext does not fit in the buffer.")
            );
        }

        Interlocked.Increment(ref _decryptionCount);
        return ValueTask.FromResult(bytesWritten);
    }

    /// <inheritdoc />
    public ValueTask<int> DeriveSharedSecretAsync(
        RemoteKeyAgreementRequest request,
        Memory<byte> sharedSecret,
        CancellationToken cancellationToken
    )
    {
        if (!Keys.TryGetValue(request.KeyId, out var key))
        {
            return ValueTask.FromException<int>(
                new InvalidOperationException($"The key '{request.KeyId}' does not exist.")
            );
        }

        if (key is not ECDiffieHellman ecdh)
        {
            return ValueTask.FromException<int>(
                new InvalidOperationException($"The key '{request.KeyId}' cannot agree.")
            );
        }

        using var peer = ECDiffieHellman.Create();
        peer.ImportSubjectPublicKeyInfo(request.PeerSubjectPublicKeyInfo.Span, out _);

        var secret = ecdh.DeriveRawSecretAgreement(peer.PublicKey);
        try
        {
            if (secret.Length > sharedSecret.Length)
            {
                return ValueTask.FromException<int>(
                    new CryptographicException("The shared secret does not fit in the buffer.")
                );
            }

            secret.CopyTo(sharedSecret);
            Interlocked.Increment(ref _agreementCount);
            return ValueTask.FromResult(secret.Length);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<RemoteKeyListing> ListKeysAsync(
        RemoteKeyListRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await Task.Yield();

        foreach (var (keyId, entry) in Listings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (
                Keys.ContainsKey(keyId)
                && string.Equals(entry.Deployment, request.Deployment, StringComparison.Ordinal)
            )
            {
                yield return entry.Listing;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DestroyKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        if (Keys.TryRemove(keyId, out var key))
        {
            key.Dispose();
        }

        DestroyedKeys.Enqueue(keyId);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var keyId in Keys.Keys)
        {
            if (Keys.TryRemove(keyId, out var key))
            {
                key.Dispose();
            }
        }
    }

    private static RemoteSigningAlgorithm CreateRsaRow(string algorithm, string signingAlgorithm) =>
        new()
        {
            Provider = ProviderName,
            Algorithm = algorithm,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
            KeySpec = "RSA_3072",
            SigningAlgorithm = signingAlgorithm,
            DigestAlgorithm = HashAlgorithmName.SHA256,
        };
}
