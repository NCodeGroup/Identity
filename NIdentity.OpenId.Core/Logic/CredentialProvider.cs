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
using Microsoft.Extensions.Primitives;
using NCode.Jose;
using NCode.Jose.Algorithms;
using NCode.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Jose.Algorithms.Compression;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Algorithms.Signature;
using NCode.Jose.Extensions;
using NCode.Jose.SecretKeys;

namespace NIdentity.OpenId.Logic;

public interface ICredentialProvider
{
    IReadOnlyCollection<ISignatureAlgorithm> SupportedSignatureAlgorithms { get; }
    IReadOnlyCollection<IKeyManagementAlgorithm> SupportedKeyManagementAlgorithms { get; }
    IReadOnlyCollection<IAuthenticatedEncryptionAlgorithm> SupportedAuthenticatedEncryptionAlgorithms { get; }
    IReadOnlyCollection<ICompressionAlgorithm> SupportedCompressionAlgorithms { get; }
}

public class CredentialProvider : IDisposable, ICredentialProvider
{
    private ISecretKeyProvider SecretKeyProvider { get; }
    private IAlgorithmProvider AlgorithmProvider { get; }

    private IList<IDisposable> ChangeRegistrations { get; } = new List<IDisposable>();

    public IReadOnlyCollection<ISignatureAlgorithm> SupportedSignatureAlgorithms { get; private set; }
    public IReadOnlyCollection<IKeyManagementAlgorithm> SupportedKeyManagementAlgorithms { get; private set; }
    public IReadOnlyCollection<IAuthenticatedEncryptionAlgorithm> SupportedAuthenticatedEncryptionAlgorithms { get; private set; }
    public IReadOnlyCollection<ICompressionAlgorithm> SupportedCompressionAlgorithms { get; private set; }

    public CredentialProvider(ISecretKeyProvider secretKeyProvider, IAlgorithmProvider algorithmProvider)
    {
        SecretKeyProvider = secretKeyProvider;
        AlgorithmProvider = algorithmProvider;

        HandleAlgorithmChange();

        ChangeRegistrations.Add(ChangeToken.OnChange(algorithmProvider.GetChangeToken, HandleAlgorithmChange));
    }

    public void Dispose()
    {
        ChangeRegistrations.DisposeAll(ignoreExceptions: true);
    }

    private void HandleAlgorithmChange()
    {
        var signatureAlgorithms = new List<ISignatureAlgorithm>();
        var keyManagementAlgorithms = new List<IKeyManagementAlgorithm>();
        var authenticatedEncryptionAlgorithms = new List<IAuthenticatedEncryptionAlgorithm>();
        var compressionAlgorithms = new List<ICompressionAlgorithm>();

        foreach (var algorithm in AlgorithmProvider.Algorithms)
        {
            switch (algorithm)
            {
                case ISignatureAlgorithm signatureAlgorithm:
                    signatureAlgorithms.Add(signatureAlgorithm);
                    break;

                case IKeyManagementAlgorithm keyManagementAlgorithm:
                    keyManagementAlgorithms.Add(keyManagementAlgorithm);
                    break;

                case IAuthenticatedEncryptionAlgorithm authenticatedEncryptionAlgorithm:
                    authenticatedEncryptionAlgorithms.Add(authenticatedEncryptionAlgorithm);
                    break;

                case ICompressionAlgorithm compressionAlgorithm:
                    compressionAlgorithms.Add(compressionAlgorithm);
                    break;
            }
        }

        SupportedSignatureAlgorithms = signatureAlgorithms;
        SupportedKeyManagementAlgorithms = keyManagementAlgorithms;
        SupportedAuthenticatedEncryptionAlgorithms = authenticatedEncryptionAlgorithms;
        SupportedCompressionAlgorithms = compressionAlgorithms;
    }

    //private TokenPreferredAlgorithms IdTokenPreferredAlgorithms { get; } = new();

    private static IEnumerable<string> GetIntersection(
        IEnumerable<string> aRequired,
        IReadOnlyCollection<string>? bOptional) =>
        bOptional is null || bOptional.Count == 0 ? aRequired : aRequired.Intersect(bOptional);

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
        IReadOnlyCollection<string>? allowed,
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

    public bool TryGetSigningCredentials(
        IEnumerable<string> supported,
        IReadOnlyCollection<string>? allowed,
        [NotNullWhen(true)] out JoseSigningCredentials? credentials)
    {
        if (!TryGetCredentials<ISignatureAlgorithm>("sig", supported, allowed, out var tuple))
        {
            credentials = null;
            return false;
        }

        credentials = new JoseSigningCredentials(tuple.Item1, tuple.Item2);
        return true;
    }

    public bool TryGetEncryptionCredentials(
        AlgorithmSet supported,
        AlgorithmSet? allowed,
        [NotNullWhen(true)] out JoseEncryptionCredentials? credentials)
    {
        if (!TryGetCredentials<IKeyManagementAlgorithm>(
                "enc",
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
        if (!TryGetAlgorithm<IAuthenticatedEncryptionAlgorithm>(supportedEncryptionCodes, out var algorithmEncryption))
        {
            credentials = null;
            return false;
        }

        var supportedCompressionCodes = GetIntersection(
            supported.CompressionAlgorithms,
            allowed?.CompressionAlgorithms);
        TryGetAlgorithm<ICompressionAlgorithm>(supportedCompressionCodes, out var algorithmCompression);

        credentials = new JoseEncryptionCredentials(
            tuple.Item1,
            tuple.Item2,
            algorithmEncryption,
            algorithmCompression);
        return true;
    }

    public JoseEncodeCredentials GetEncodeCredentials(
        AlgorithmSet supported,
        AlgorithmSet? allowed,
        bool requireEncryption = false)
    {
        if (!requireEncryption &&
            TryGetSigningCredentials(
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

public class AlgorithmSet
{
    public List<string> SigningAlgorithms { get; set; } = new();
    public List<string> KeyManagementAlgorithms { get; set; } = new();
    public List<string> EncryptionAlgorithms { get; set; } = new();
    public List<string> CompressionAlgorithms { get; set; } = new();
}
