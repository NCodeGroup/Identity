// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using AlgorithmType = NCode.Identity.Jose.Algorithms.AlgorithmType;
using IAlgorithmCollection = NCode.Identity.Jose.Algorithms.IAlgorithmCollection;
using KeyedAlgorithm = NCode.Identity.Jose.Algorithms.KeyedAlgorithm;
using KeyManagementAlgorithm = NCode.Identity.Jose.Algorithms.KeyManagementAlgorithm;
using SignatureAlgorithm = NCode.Identity.Jose.Algorithms.SignatureAlgorithm;

namespace NCode.Identity.Jose.Credentials;

/// <summary>
/// Provides the default implementation of the <see cref="ICredentialSelector"/> abstraction. A key is selected only
/// while it is in its active window: a key published ahead of its activation, a retired key and an expired key never
/// sign or encrypt, whatever algorithm they fit.
/// </summary>
// See ADR-1403.
internal class DefaultCredentialSelector(TimeProvider timeProvider) : ICredentialSelector
{
    private TimeProvider TimeProvider { get; } = timeProvider;

    private static bool IsSecretKeyUseCompatible(
        Secrets.Keys.SecretKey secretKey,
        string expectedUse
    ) => secretKey.Metadata.Use is null || secretKey.Metadata.Use == expectedUse;

    private static bool IsSecretKeyAlgorithmCompatible(
        Secrets.Keys.SecretKey secretKey,
        string algorithmCode
    ) => secretKey.Metadata.Algorithm is null || secretKey.Metadata.Algorithm == algorithmCode;

    private static bool IsSecretKeyCompatible(
        Secrets.Keys.SecretKey secretKey,
        KeyedAlgorithm algorithm,
        string expectedUse,
        DateTimeOffset now
    ) =>
        secretKey.Metadata.IsActive(now)
        && IsSecretKeyUseCompatible(secretKey, expectedUse)
        && IsSecretKeyAlgorithmCompatible(secretKey, algorithm.Code)
        && algorithm.IsKeyCompatible(secretKey);

    private static bool TryGetAlgorithm<T>(
        AlgorithmType algorithmType,
        IAlgorithmCollection candidateAlgorithms,
        IEnumerable<string> preferredAlgorithms,
        [MaybeNullWhen(false)] out T algorithm
    )
        where T : Algorithm
    {
        foreach (var algorithmCode in preferredAlgorithms)
        {
            if (candidateAlgorithms.TryGetAlgorithm(algorithmType, algorithmCode, out algorithm))
            {
                return true;
            }
        }

        algorithm = default;
        return false;
    }

    private bool TryGetCredential<T>(
        string expectedUse,
        AlgorithmType algorithmType,
        IAlgorithmCollection candidateAlgorithms,
        IEnumerable<string> preferredAlgorithms,
        IReadOnlyCollection<Secrets.Keys.SecretKey> candidateKeys,
        out (Secrets.Keys.SecretKey SecretKey, T Algorithm) credentials
    )
        where T : KeyedAlgorithm
    {
        var now = TimeProvider.GetUtcNow();

        foreach (var algorithmCode in preferredAlgorithms)
        {
            if (
                !candidateAlgorithms.TryGetAlgorithm<T>(
                    algorithmType,
                    algorithmCode,
                    out var algorithm
                )
            )
                continue;

            foreach (
                var secretKey in candidateKeys.Where(secretKey =>
                    IsSecretKeyCompatible(secretKey, algorithm, expectedUse, now)
                )
            )
            {
                credentials = (secretKey, algorithm);
                return true;
            }
        }

        credentials = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetSigningCredentials(
        IAlgorithmCollection candidateAlgorithms,
        IEnumerable<string> preferredSignatureAlgorithms,
        IReadOnlyCollection<SecretKey> candidateKeys,
        [MaybeNullWhen(false)] out JoseSigningCredentials credentials
    )
    {
        if (
            !TryGetCredential<SignatureAlgorithm>(
                SecretKeyUses.Signature,
                AlgorithmType.DigitalSignature,
                candidateAlgorithms,
                preferredSignatureAlgorithms,
                candidateKeys,
                out var signing
            )
        )
        {
            credentials = null;
            return false;
        }

        credentials = new JoseSigningCredentials(signing.SecretKey, signing.Algorithm);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetEncryptionCredentials(
        IAlgorithmCollection candidateAlgorithms,
        IEnumerable<string> preferredKeyManagementAlgorithms,
        IEnumerable<string> preferredAuthenticatedEncryptionAlgorithms,
        IEnumerable<string> preferredCompressionAlgorithms,
        IReadOnlyCollection<SecretKey> candidateKeys,
        [MaybeNullWhen(false)] out JoseEncryptionCredentials credentials
    )
    {
        if (
            !TryGetCredential<KeyManagementAlgorithm>(
                SecretKeyUses.Encryption,
                AlgorithmType.KeyManagement,
                candidateAlgorithms,
                preferredKeyManagementAlgorithms,
                candidateKeys,
                out var keyManagement
            )
        )
        {
            credentials = null;
            return false;
        }

        if (
            !TryGetAlgorithm<AuthenticatedEncryptionAlgorithm>(
                AlgorithmType.AuthenticatedEncryption,
                candidateAlgorithms,
                preferredAuthenticatedEncryptionAlgorithms,
                out var authenticatedEncryptionAlgorithm
            )
        )
        {
            credentials = null;
            return false;
        }

        TryGetAlgorithm<CompressionAlgorithm>(
            AlgorithmType.Compression,
            candidateAlgorithms,
            preferredCompressionAlgorithms,
            out var compressionAlgorithm
        );

        credentials = new JoseEncryptionCredentials(
            keyManagement.SecretKey,
            keyManagement.Algorithm,
            authenticatedEncryptionAlgorithm,
            compressionAlgorithm
        );
        return true;
    }
}
