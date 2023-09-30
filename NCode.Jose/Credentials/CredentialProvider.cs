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
        [NotNullWhen(true)] out JoseSignatureCredentials? credentials);

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
        [NotNullWhen(true)] out JoseEncryptionCredentials? credentials);

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
        if (bOptional is null)
            return aRequired;

        if (bOptional.TryGetNonEnumeratedCount(out var count) && count == 0)
            return aRequired;

        var bCollection = bOptional as IReadOnlyCollection<string> ?? bOptional.ToList();
        return bCollection.Count == 0 ? aRequired : aRequired.Intersect(bCollection);
    }

    private static bool IsSecretKeySupported(
        string expectedUse,
        KeyMetadata keyMetadata,
        IReadOnlySet<string> algorithmCodes) =>
        (keyMetadata.Use is null || keyMetadata.Use == expectedUse) &&
        (keyMetadata.Algorithm is null || algorithmCodes.Contains(keyMetadata.Algorithm));

    private static bool IsSecretKeyCompatible(
        IKeyedAlgorithm algorithm,
        SecretKey secretKey) =>
        algorithm.KeyType.IsInstanceOfType(secretKey) &&
        KeySizesUtility.IsLegalSize(algorithm.KeyBitSizes, secretKey.KeySizeBits);

    private bool TryGetAlgorithm<T>(
        IEnumerable<string> algorithmCodes,
        [NotNullWhen(true)] out T? algorithm)
        where T : IAlgorithm
    {
        var query =
            from algorithmCode in algorithmCodes
            join algorithmToCheck in AlgorithmProvider.Algorithms.OfType<T>()
                on algorithmCode equals algorithmToCheck.Code
            select algorithmToCheck;

        algorithm = query.FirstOrDefault();
        return algorithm != null;
    }

    private bool TryGetCredentials<T>(
        string expectedUse,
        IEnumerable<string> supported,
        IEnumerable<string>? allowed,
        [NotNullWhen(true)] out Tuple<SecretKey, T>? credentials)
        where T : IKeyedAlgorithm
    {
        var algorithmCodes = GetIntersection(supported, allowed)
            .ToHashSet();

        var secretKeys = SecretKeyProvider.SecretKeys
            .Where(key => IsSecretKeySupported(expectedUse, key.Metadata, algorithmCodes))
            .OrderByDescending(key => key.Metadata.ExpiresWhen ?? DateTimeOffset.MaxValue);

        var algorithms = AlgorithmProvider.Algorithms
            .OfType<T>()
            .Where(algorithm => algorithmCodes.Contains(algorithm.Code));

        var query =
            from secretKey in secretKeys
            from algorithm in algorithms
            where IsSecretKeyCompatible(algorithm, secretKey)
            select Tuple.Create(secretKey, algorithm);

        credentials = query.FirstOrDefault();
        return credentials != null;
    }

    /// <inheritdoc />
    public bool TryGetSignatureCredentials(
        IEnumerable<string> supported,
        IEnumerable<string>? allowed,
        [NotNullWhen(true)] out JoseSignatureCredentials? credentials)
    {
        if (!TryGetCredentials<ISignatureAlgorithm>(
                SecretKeyUses.Signature,
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
        [NotNullWhen(true)] out JoseEncryptionCredentials? credentials)
    {
        if (!TryGetCredentials<IKeyManagementAlgorithm>(
                SecretKeyUses.Encryption,
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

        if (!TryGetAlgorithm<IAuthenticatedEncryptionAlgorithm>(
                supportedEncryptionCodes,
                out var algorithmEncryption))
        {
            credentials = null;
            return false;
        }

        var supportedCompressionCodes = GetIntersection(
            supported.CompressionAlgorithms,
            allowed?.CompressionAlgorithms);

        TryGetAlgorithm<ICompressionAlgorithm>(
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

        throw new InvalidOperationException();
    }
}
