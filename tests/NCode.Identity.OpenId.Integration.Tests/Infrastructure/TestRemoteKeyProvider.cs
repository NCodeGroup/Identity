// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Security.Cryptography;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A remote key provider that stands in for a key management service: it holds each key it creates in memory, signs
/// the digest it receives the way a service does (RSASSA-PSS for <c>PS256</c>, PKCS #1 v1.5 for <c>RS256</c> and a DER
/// <c>SEQUENCE</c> for <c>ES256</c>), and counts every signature and every destruction so a test can tell that the
/// service signed or destroyed a key.
/// </summary>
public sealed class TestRemoteKeyProvider
    : IRemoteKeyProvider,
        IRemoteSigningAlgorithmSource,
        IDisposable
{
    /// <summary>
    /// The name that identifies this provider.
    /// </summary>
    public const string ProviderName = "test-kms";

    private const string KeyIdPrefix = "test-kms-key-";

    private static readonly IReadOnlyList<RemoteSigningAlgorithm> Rows =
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
    ];

    private ConcurrentDictionary<string, AsymmetricAlgorithm> Keys { get; } =
        new(StringComparer.Ordinal);

    private ConcurrentQueue<string> CreatedKeys { get; } = new();

    private ConcurrentQueue<string> DestroyedKeys { get; } = new();

    private long _signatureCount;

    /// <inheritdoc />
    public string Name => ProviderName;

    /// <inheritdoc />
    public IEnumerable<RemoteSigningAlgorithm> Algorithms => Rows;

    /// <summary>
    /// Gets the number of signatures that the provider produced.
    /// </summary>
    public long SignatureCount => Interlocked.Read(ref _signatureCount);

    /// <summary>
    /// Gets the identifier of every key that the provider created, in order.
    /// </summary>
    public IReadOnlyList<string> CreatedKeyIds => [.. CreatedKeys];

    /// <summary>
    /// Gets the identifier of every key that the provider destroyed, in order.
    /// </summary>
    public IReadOnlyList<string> DestroyedKeyIds => [.. DestroyedKeys];

    /// <inheritdoc />
    public ValueTask<RemoteKeyCreationResult> CreateKeyAsync(
        RemoteKeyCreationRequest request,
        CancellationToken cancellationToken
    )
    {
        var algorithm = request.Algorithm;
        AsymmetricAlgorithm key = algorithm.SecretType switch
        {
            SecretKeyTypes.Rsa => RSA.Create(algorithm.KeySizeBits),
            SecretKeyTypes.Ecc => ECDsa.Create(ECCurve.NamedCurves.nistP256),
            _ => throw new InvalidOperationException(
                $"The provider holds no '{algorithm.SecretType}' key."
            ),
        };

        var keyId = $"{KeyIdPrefix}{Guid.NewGuid():N}";
        Keys[keyId] = key;
        CreatedKeys.Enqueue(keyId);

        return ValueTask.FromResult(
            new RemoteKeyCreationResult
            {
                KeyId = keyId,
                SubjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo(),
            }
        );
    }

    /// <inheritdoc />
    public ValueTask<byte[]> SignAsync(
        RemoteSignatureRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!Keys.TryGetValue(request.KeyId, out var key))
        {
            throw new InvalidOperationException($"The key '{request.KeyId}' does not exist.");
        }

        var digest =
            request.Algorithm.DigestAlgorithm
            ?? throw new InvalidOperationException("The provider signs a digest.");

        var signature = key switch
        {
            RSA rsa => rsa.SignHash(
                request.Data.Span,
                digest,
                request.Algorithm.Algorithm.StartsWith("PS", StringComparison.Ordinal)
                    ? RSASignaturePadding.Pss
                    : RSASignaturePadding.Pkcs1
            ),
            ECDsa ecdsa => ecdsa.SignHash(request.Data.Span, DSASignatureFormat.Rfc3279DerSequence),
            _ => throw new InvalidOperationException($"The key '{request.KeyId}' cannot sign."),
        };

        Interlocked.Increment(ref _signatureCount);
        return ValueTask.FromResult(signature);
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
