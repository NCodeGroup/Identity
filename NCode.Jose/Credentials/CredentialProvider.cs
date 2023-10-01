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
/// Provides the ability to retrieve <see cref="JoseSignatureCredentials"/> and <see cref="JoseEncryptionCredentials"/>
/// based on criteria that specify supported and allowed algorithms.
/// </summary>
public interface ICredentialProvider
{
    /// <summary>
    /// Attempts to retrieve <see cref="JoseSignatureCredentials"/> based on the specified criteria.
    /// </summary>
    /// <param name="supported">A collection of algorithms codes that will be considered.</param>
    /// <param name="allowed">A optional collection of algorithm codes that restrict the list of supported algorithms.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="JoseSignatureCredentials"/> that meet the specified criteria.</param>
    /// <returns><c>true</c> if signing credentials were found that match the specified criteria; otherwise, <c>false</c>.</returns>
    bool TryGetSignatureCredentials(
        IEnumerable<string> supported,
        IEnumerable<string>? allowed,
        [MaybeNullWhen(false)] out JoseSignatureCredentials credentials);

    /// <summary>
    /// Attempts to retrieve <see cref="JoseEncryptionCredentials"/> based on the specified criteria.
    /// </summary>
    /// <param name="supported">A collection of algorithms codes that will be considered.</param>
    /// <param name="allowed">A optional collection of algorithm codes that restrict the list of supported algorithms.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="JoseEncryptionCredentials"/> that meet the specified criteria.</param>
    /// <returns><c>true</c> if encryption credentials were found that match the specified criteria; otherwise, <c>false</c>.</returns>
    bool TryGetEncryptionCredentials(
        AlgorithmSet supported,
        AlgorithmSet? allowed,
        [MaybeNullWhen(false)] out JoseEncryptionCredentials credentials);

    /// <summary>
    /// Gets the <see cref="JoseEncodeCredentials"/> that meet the specified criteria.
    /// </summary>
    /// <param name="supported">A collection of algorithms codes that will be considered.</param>
    /// <param name="allowed">A optional collection of algorithm codes that restrict the list of supported algorithms.</param>
    /// <param name="requireEncryption"><c>true</c> whether only encryption credentials should be returned; otherwise, <c>false</c> when either signing credentials or encryption credentials should be returned.</param>
    /// <returns>The <see cref="JoseEncodeCredentials"/> that can be used to encode a JOSE token.</returns>
    JoseEncodeCredentials GetEncodeCredentials(
        AlgorithmSet supported,
        AlgorithmSet? allowed,
        bool requireEncryption = false);
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

    private static IEnumerable<string> GetIntersection(
        IEnumerable<string> aRequired,
        IEnumerable<string>? bOptional)
    {
        // NOTE: we must preserve the order of 'b', if present, otherwise 'a'

        if (bOptional is null)
            return aRequired;

        if (bOptional.TryGetNonEnumeratedCount(out var count) && count == 0)
            return aRequired;

        var bCollection = bOptional as IReadOnlyCollection<string> ?? bOptional.ToList();
        return bCollection.Count == 0 ? aRequired : bCollection.Intersect(aRequired);
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
        IEnumerable<string> algorithmCodes,
        [MaybeNullWhen(false)] out T algorithm)
        where T : IAlgorithm
    {
        // NOTE: we must preserve the order of algorithms

        foreach (var algorithmCode in algorithmCodes)
        {
            if (AlgorithmProvider.Algorithms.TryGetAlgorithm(algorithmType, algorithmCode, out algorithm))
            {
                return true;
            }
        }

        algorithm = default;
        return false;
    }

    private bool TryGetFirstCredential<T>(
        string expectedUse,
        AlgorithmType algorithmType,
        IEnumerable<string> supported,
        IEnumerable<string>? allowed,
        [MaybeNullWhen(false)] out Tuple<SecretKey, T> credentials)
        where T : IKeyedAlgorithm
    {
        // NOTE: we must preserve the order of algorithms

        var algorithmCodes = GetIntersection(supported, allowed);

        foreach (var algorithmCode in algorithmCodes)
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
    public bool TryGetSignatureCredentials(
        IEnumerable<string> supported,
        IEnumerable<string>? allowed,
        [MaybeNullWhen(false)] out JoseSignatureCredentials credentials)
    {
        if (!TryGetFirstCredential<ISignatureAlgorithm>(
                SecretKeyUses.Signature,
                AlgorithmType.DigitalSignature,
                supported,
                allowed,
                out var tuple))
        {
            credentials = null;
            return false;
        }

        credentials = new JoseSignatureCredentials(tuple.Item1, tuple.Item2);
        return true;
    }

    /// <inheritdoc />
    public bool TryGetEncryptionCredentials(
        AlgorithmSet supported,
        AlgorithmSet? allowed,
        [MaybeNullWhen(false)] out JoseEncryptionCredentials credentials)
    {
        if (!TryGetFirstCredential<IKeyManagementAlgorithm>(
                SecretKeyUses.Encryption,
                AlgorithmType.KeyManagement,
                supported.KeyManagementAlgorithms,
                allowed?.KeyManagementAlgorithms,
                out var tuple))
        {
            credentials = null;
            return false;
        }

        var supportedEncryptionCodes = GetIntersection(
            supported.EncryptionAlgorithms,
            allowed?.EncryptionAlgorithms);

        if (!TryGetFirstAlgorithm<IAuthenticatedEncryptionAlgorithm>(
                AlgorithmType.AuthenticatedEncryption,
                supportedEncryptionCodes,
                out var algorithmEncryption))
        {
            credentials = null;
            return false;
        }

        var supportedCompressionCodes = GetIntersection(
            supported.CompressionAlgorithms,
            allowed?.CompressionAlgorithms);

        TryGetFirstAlgorithm<ICompressionAlgorithm>(
            AlgorithmType.Compression,
            supportedCompressionCodes,
            out var algorithmCompression);

        credentials = new JoseEncryptionCredentials(
            tuple.Item1,
            tuple.Item2,
            algorithmEncryption,
            algorithmCompression);
        return true;
    }

    /// <inheritdoc />
    public JoseEncodeCredentials GetEncodeCredentials(
        AlgorithmSet supported,
        AlgorithmSet? allowed,
        bool requireEncryption = false)
    {
        if (!requireEncryption &&
            TryGetSignatureCredentials(
                supported.SigningAlgorithms,
                allowed?.SigningAlgorithms,
                out var signingCredentials))
            return signingCredentials;

        if (TryGetEncryptionCredentials(
                supported,
                allowed,
                out var encryptionCredentials))
            return encryptionCredentials;

        // TODO: better exception
        throw new InvalidOperationException();
    }
}
