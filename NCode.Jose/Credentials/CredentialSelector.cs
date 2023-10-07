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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using NCode.Jose.Algorithms;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Credentials;

/// <summary>
/// Provides the default implementation of the <see cref="ICredentialSelector"/> abstraction.
/// </summary>
public class CredentialSelector : ICredentialSelector
{
    private IAlgorithmProvider AlgorithmProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialSelector"/> class.
    /// </summary>
    public CredentialSelector(IAlgorithmProvider algorithmProvider)
    {
        AlgorithmProvider = algorithmProvider;
    }

    private static bool IsSecretKeyUseCompatible(SecretKey secretKey, string expectedUse) =>
        secretKey.Metadata.Use is null || secretKey.Metadata.Use == expectedUse;

    private static bool IsSecretKeyAlgorithmCompatible(SecretKey secretKey, string algorithmCode) =>
        secretKey.Metadata.Algorithm is null || secretKey.Metadata.Algorithm == algorithmCode;

    private static bool IsSecretKeyTypeCompatible(SecretKey secretKey, Type keyType) =>
        keyType.IsInstanceOfType(secretKey);

    private static bool IsSecretKeySizeCompatible(SecretKey secretKey, IEnumerable<KeySizes> legalBitSizes) =>
        KeySizesUtility.IsLegalSize(legalBitSizes, secretKey.KeySizeBits);

    private static bool IsSecretKeyCompatible(SecretKey secretKey, KeyedAlgorithm algorithm, string expectedUse) =>
        IsSecretKeyUseCompatible(secretKey, expectedUse) &&
        IsSecretKeyAlgorithmCompatible(secretKey, algorithm.Code) &&
        IsSecretKeyTypeCompatible(secretKey, algorithm.KeyType) &&
        IsSecretKeySizeCompatible(secretKey, algorithm.KeyBitSizes);

    private bool TryGetAlgorithm<T>(
        AlgorithmType algorithmType,
        IEnumerable<string> preferredAlgorithms,
        [MaybeNullWhen(false)] out T algorithm)
        where T : Algorithm
    {
        foreach (var algorithmCode in preferredAlgorithms)
        {
            if (AlgorithmProvider.Algorithms.TryGetAlgorithm(algorithmType, algorithmCode, out algorithm))
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
        IEnumerable<string> preferredAlgorithms,
        IReadOnlyCollection<SecretKey> secretKeyCollection,
        [MaybeNullWhen(false)] out Tuple<SecretKey, T> credentials)
        where T : KeyedAlgorithm
    {
        foreach (var algorithmCode in preferredAlgorithms)
        {
            if (!AlgorithmProvider.Algorithms.TryGetAlgorithm<T>(algorithmType, algorithmCode, out var algorithm))
                continue;

            foreach (var secretKey in secretKeyCollection.Where(secretKey => IsSecretKeyCompatible(secretKey, algorithm, expectedUse)))
            {
                credentials = Tuple.Create(secretKey, algorithm);
                return true;
            }
        }

        credentials = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetSigningCredentials(
        IEnumerable<string> preferredSignatureAlgorithms,
        IReadOnlyCollection<SecretKey> secretKeyCollection,
        [MaybeNullWhen(false)] out JoseSigningCredentials credentials)
    {
        if (!TryGetCredential<SignatureAlgorithm>(
                SecretKeyUses.Signature,
                AlgorithmType.DigitalSignature,
                preferredSignatureAlgorithms,
                secretKeyCollection, out var tuple))
        {
            credentials = null;
            return false;
        }

        credentials = new JoseSigningCredentials(tuple.Item1, tuple.Item2);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetEncryptingCredentials(
        IEnumerable<string> preferredKeyManagementAlgorithms,
        IEnumerable<string> preferredEncryptionAlgorithms,
        IEnumerable<string> preferredCompressionAlgorithms,
        IReadOnlyCollection<SecretKey> secretKeyCollection,
        [MaybeNullWhen(false)] out JoseEncryptingCredentials credentials)
    {
        if (!TryGetCredential<KeyManagementAlgorithm>(
                SecretKeyUses.Encryption,
                AlgorithmType.KeyManagement,
                preferredKeyManagementAlgorithms,
                secretKeyCollection,
                out var tuple))
        {
            credentials = null;
            return false;
        }

        if (!TryGetAlgorithm<AuthenticatedEncryptionAlgorithm>(
                AlgorithmType.AuthenticatedEncryption,
                preferredEncryptionAlgorithms,
                out var encryptionAlgorithm))
        {
            credentials = null;
            return false;
        }

        TryGetAlgorithm<CompressionAlgorithm>(
            AlgorithmType.Compression,
            preferredCompressionAlgorithms,
            out var compressionAlgorithm);

        credentials = new JoseEncryptingCredentials(
            tuple.Item1,
            tuple.Item2,
            encryptionAlgorithm,
            compressionAlgorithm);
        return true;
    }
}
