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
using NCode.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Jose.Algorithms.Compression;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Algorithms.Signature;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Credentials;

/// <summary>
/// Provides the ability to retrieve <see cref="JoseSigningCredentials"/> and <see cref="JoseEncryptingCredentials"/>
/// based on criteria that specify supported and allowed algorithms.
/// </summary>
public interface ICredentialProvider
{
    /// <summary>
    /// Attempts to retrieve <see cref="JoseSigningCredentials"/> based on the specified criteria.
    /// </summary>
    /// <param name="preferredSignatureAlgorithmCodes">An ordered collection of signature algorithms codes that will be considered.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="JoseSigningCredentials"/> that meet the specified criteria.</param>
    /// <returns><c>true</c> if signing credentials were found that match the specified criteria; otherwise, <c>false</c>.</returns>
    bool TryGetSigningCredentials(
        IEnumerable<string> preferredSignatureAlgorithmCodes,
        [MaybeNullWhen(false)] out JoseSigningCredentials credentials);

    /// <summary>
    /// Attempts to retrieve <see cref="JoseEncryptingCredentials"/> based on the specified criteria.
    /// </summary>
    /// <param name="preferredKeyManagementAlgorithmCodes">An ordered collection of key management algorithms codes that will be considered.</param>
    /// <param name="preferredEncryptionAlgorithmCodes">An ordered collection of encryption algorithms codes that will be considered.</param>
    /// <param name="preferredCompressionAlgorithmCodes">An ordered collection of compression algorithms codes that will be considered.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="JoseEncryptingCredentials"/> that meet the specified criteria.</param>
    /// <returns><c>true</c> if encryption credentials were found that match the specified criteria; otherwise, <c>false</c>.</returns>
    bool TryGetEncryptingCredentials(
        IEnumerable<string> preferredKeyManagementAlgorithmCodes,
        IEnumerable<string> preferredEncryptionAlgorithmCodes,
        IEnumerable<string> preferredCompressionAlgorithmCodes,
        [MaybeNullWhen(false)] out JoseEncryptingCredentials credentials);
}

/// <summary>
/// Provides the default implementation of the <see cref="ICredentialProvider"/> interface.
/// </summary>
public class CredentialProvider : ICredentialProvider
{
    private ISecretKeyProvider SecretKeyProvider { get; }
    private IAlgorithmProvider AlgorithmProvider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialProvider"/> class.
    /// </summary>
    /// <param name="secretKeyProvider">The <see cref="ISecretKeyProvider"/> instance that provides <see cref="SecretKey"/> instances.</param>
    /// <param name="algorithmProvider">The <see cref="IAlgorithmProvider"/> instance that provides <see cref="IAlgorithm"/> instances.</param>
    public CredentialProvider(ISecretKeyProvider secretKeyProvider, IAlgorithmProvider algorithmProvider)
    {
        SecretKeyProvider = secretKeyProvider;
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

    private static bool IsSecretKeyCompatible(SecretKey secretKey, IKeyedAlgorithm algorithm, string expectedUse) =>
        IsSecretKeyUseCompatible(secretKey, expectedUse) &&
        IsSecretKeyAlgorithmCompatible(secretKey, algorithm.Code) &&
        IsSecretKeyTypeCompatible(secretKey, algorithm.KeyType) &&
        IsSecretKeySizeCompatible(secretKey, algorithm.KeyBitSizes);

    private bool TryGetFirstAlgorithm<T>(
        AlgorithmType algorithmType,
        IEnumerable<string> preferredAlgorithmCodes,
        [MaybeNullWhen(false)] out T algorithm)
        where T : IAlgorithm
    {
        // NOTE: we must preserve the order of preferred algorithms

        foreach (var algorithmCode in preferredAlgorithmCodes)
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
        IEnumerable<string> preferredAlgorithmCodes,
        [MaybeNullWhen(false)] out Tuple<SecretKey, T> credentials)
        where T : IKeyedAlgorithm
    {
        // NOTE: we must preserve the order of preferred algorithms

        foreach (var algorithmCode in preferredAlgorithmCodes)
        {
            if (!AlgorithmProvider.Algorithms.TryGetAlgorithm<T>(algorithmType, algorithmCode, out var algorithm))
                continue;

            // TODO: can we optimize this to not enumerate all keys repeatedly?
            var secretKeys = SecretKeyProvider.SecretKeys
                .OrderByDescending(key => key.Metadata.ExpiresWhen ?? DateTimeOffset.MaxValue);

            foreach (var secretKey in secretKeys)
            {
                if (!IsSecretKeyCompatible(secretKey, algorithm, expectedUse))
                    continue;

                credentials = Tuple.Create(secretKey, algorithm);
                return true;
            }
        }

        credentials = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetSigningCredentials(
        IEnumerable<string> preferredSignatureAlgorithmCodes,
        [MaybeNullWhen(false)] out JoseSigningCredentials credentials)
    {
        if (!TryGetCredential<ISignatureAlgorithm>(
                SecretKeyUses.Signature,
                AlgorithmType.DigitalSignature,
                preferredSignatureAlgorithmCodes,
                out var tuple))
        {
            credentials = null;
            return false;
        }

        credentials = new JoseSigningCredentials(tuple.Item1, tuple.Item2);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetEncryptingCredentials(
        IEnumerable<string> preferredKeyManagementAlgorithmCodes,
        IEnumerable<string> preferredEncryptionAlgorithmCodes,
        IEnumerable<string> preferredCompressionAlgorithmCodes,
        [MaybeNullWhen(false)] out JoseEncryptingCredentials credentials)
    {
        if (!TryGetCredential<IKeyManagementAlgorithm>(
                SecretKeyUses.Encryption,
                AlgorithmType.KeyManagement,
                preferredKeyManagementAlgorithmCodes,
                out var tuple))
        {
            credentials = null;
            return false;
        }

        if (!TryGetFirstAlgorithm<IAuthenticatedEncryptionAlgorithm>(
                AlgorithmType.AuthenticatedEncryption,
                preferredEncryptionAlgorithmCodes,
                out var encryptionAlgorithm))
        {
            credentials = null;
            return false;
        }

        TryGetFirstAlgorithm<ICompressionAlgorithm>(
            AlgorithmType.Compression,
            preferredCompressionAlgorithmCodes,
            out var compressionAlgorithm);

        credentials = new JoseEncryptingCredentials(
            tuple.Item1,
            tuple.Item2,
            encryptionAlgorithm,
            compressionAlgorithm);
        return true;
    }
}
