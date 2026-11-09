// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Exceptions;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.RemoteKeys;
using Nerdbank.Streams;

namespace NCode.Identity.Jose;

/// <summary>
/// Provides the default implementation of <see cref="IRemoteKeyProver"/>. A signing key signs 32 random bytes through
/// the key management service with <see cref="SignatureAlgorithm.SignAsync"/>, which verifies the service's signature
/// with the key's public half. An encryption key decrypts 32 random bytes that are encrypted to the public half as a
/// compact JWE (<c>A256GCM</c>). A signature or a decryption that does not prove the key is a
/// <see cref="RemoteKeyRefusedException"/>; any other failure propagates.
/// </summary>
// See ADR-1475.
// See ADR-1477.
// See ADR-1479.
internal sealed class DefaultRemoteKeyProver(
    IJoseSerializer joseSerializer,
    IAlgorithmCollectionProvider algorithmCollectionProvider
) : IRemoteKeyProver
{
    private const int ChallengeSizeBytes = 32;

    private IJoseSerializer JoseSerializer { get; } = joseSerializer;

    private IAlgorithmCollectionProvider AlgorithmCollectionProvider { get; } =
        algorithmCollectionProvider;

    /// <inheritdoc />
    public async ValueTask ProveAsync(SecretKey secretKey, CancellationToken cancellationToken)
    {
        var remoteKey =
            secretKey as IRemoteSecretKey
            ?? throw new ArgumentException(
                "The key is not held by a key management service.",
                nameof(secretKey)
            );

        var reference = remoteKey.Binding.Reference;
        var code = remoteKey.Binding.Algorithm.Algorithm;

        if (
            !AlgorithmCollectionProvider.Collection.TryGetKeyedAlgorithm(
                code,
                out var keyedAlgorithm,
                out _
            )
        )
        {
            throw NotRegistered(code);
        }

        switch (keyedAlgorithm)
        {
            case SignatureAlgorithm signatureAlgorithm:
                await ProveSigningAsync(
                    secretKey,
                    signatureAlgorithm,
                    reference,
                    cancellationToken
                );
                break;

            case KeyManagementAlgorithm keyManagementAlgorithm:
                await ProveDecryptionAsync(
                    secretKey,
                    keyManagementAlgorithm,
                    reference,
                    cancellationToken
                );
                break;

            default:
                throw NotRegistered(code);
        }
    }

    private static RemoteKeyRefusedException NotRegistered(string code) =>
        new($"The algorithm '{code}' is not a registered signature or key-management algorithm.");

    private static async ValueTask ProveSigningAsync(
        SecretKey key,
        SignatureAlgorithm signatureAlgorithm,
        RemoteKeyReference reference,
        CancellationToken cancellationToken
    )
    {
        using var challengeLease = BufferFactory.Rent(
            ChallengeSizeBytes,
            isSensitive: false,
            out Memory<byte> challenge
        );
        RandomNumberGenerator.Fill(challenge.Span);

        using var signatureLease = BufferFactory.Rent(
            signatureAlgorithm.GetSignatureSizeBytes(key.KeySizeBits),
            isSensitive: false,
            out Memory<byte> signature
        );

        try
        {
            await signatureAlgorithm.SignAsync(key, challenge, signature, cancellationToken);
        }
        catch (Exception exception) when (exception is JoseException or CryptographicException)
        {
            throw new RemoteKeyRefusedException(
                $"The key '{reference.KeyId}' of the remote key provider '{reference.Provider}' does not sign with the private half of the public key the service returned.",
                exception
            );
        }
    }

    // See ADR-1477.
    private async ValueTask ProveDecryptionAsync(
        SecretKey key,
        KeyManagementAlgorithm keyManagementAlgorithm,
        RemoteKeyReference reference,
        CancellationToken cancellationToken
    )
    {
        using var challengeLease = BufferFactory.Rent(
            ChallengeSizeBytes,
            isSensitive: false,
            out Memory<byte> challenge
        );
        RandomNumberGenerator.Fill(challenge.Span);

        try
        {
            if (
                !AlgorithmCollectionProvider.Collection.TryGetAuthenticatedEncryptionAlgorithm(
                    AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm,
                    out var encryptionAlgorithm
                )
            )
            {
                throw new InvalidOperationException(
                    "The A256GCM content encryption algorithm is not registered."
                );
            }

            var token = await JoseSerializer
                .CreateEncoder(
                    new JoseEncryptionOptions(
                        new JoseEncryptionCredentials(
                            key,
                            keyManagementAlgorithm,
                            encryptionAlgorithm,
                            null
                        )
                    )
                )
                .EncodeAsync((ReadOnlyMemory<byte>)challenge, cancellationToken);

            using var payload = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
            await JoseSerializer.DecodeAsync(
                JoseSerializer.Parse(token),
                key,
                payload,
                cancellationToken
            );

            if (!IsChallenge(payload, challenge.Span))
            {
                throw new RemoteKeyRefusedException(DecryptionRefusalMessage(reference));
            }
        }
        catch (Exception exception) when (exception is JoseException or CryptographicException)
        {
            throw new RemoteKeyRefusedException(DecryptionRefusalMessage(reference), exception);
        }
    }

    private static bool IsChallenge(Sequence<byte> payload, ReadOnlySpan<byte> challenge)
    {
        using var lease = payload.GetSpanLease(isSensitive: false);
        return CryptographicOperations.FixedTimeEquals(lease.Span, challenge);
    }

    private static string DecryptionRefusalMessage(RemoteKeyReference reference) =>
        $"The key '{reference.KeyId}' of the remote key provider '{reference.Provider}' does not decrypt with the private half of the public key the service returned.";
}
