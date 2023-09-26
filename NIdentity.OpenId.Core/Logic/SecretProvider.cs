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

public record SigningCredential
{
}

public interface ISecretProvider
{
    IReadOnlyCollection<ISignatureAlgorithm> SupportedSignatureAlgorithms { get; }
    IReadOnlyCollection<IKeyManagementAlgorithm> SupportedKeyManagementAlgorithms { get; }
    IReadOnlyCollection<IAuthenticatedEncryptionAlgorithm> SupportedAuthenticatedEncryptionAlgorithms { get; }
    IReadOnlyCollection<ICompressionAlgorithm> SupportedCompressionAlgorithms { get; }
}

public class SecretProvider : IDisposable, ISecretProvider
{
    private ISecretKeyProvider SecretKeyProvider { get; }
    private IAlgorithmProvider AlgorithmProvider { get; }

    private IList<IDisposable> ChangeRegistrations { get; } = new List<IDisposable>();

    public IReadOnlyCollection<ISignatureAlgorithm> SupportedSignatureAlgorithms { get; private set; }
    public IReadOnlyCollection<IKeyManagementAlgorithm> SupportedKeyManagementAlgorithms { get; private set; }
    public IReadOnlyCollection<IAuthenticatedEncryptionAlgorithm> SupportedAuthenticatedEncryptionAlgorithms { get; private set; }
    public IReadOnlyCollection<ICompressionAlgorithm> SupportedCompressionAlgorithms { get; private set; }

    public SecretProvider(ISecretKeyProvider secretKeyProvider, IAlgorithmProvider algorithmProvider)
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

    public JoseEncodeParameters GetJoseParameters(IEnumerable<string> allowedAlgorithms)
    {
        var supportedKeys = SecretKeyProvider.SecretKeys;
        var supportedAlgorithms = AlgorithmProvider.Algorithms
            .OfType<IKeyedAlgorithm>()
            .Where(algorithm => allowedAlgorithms.Contains(algorithm.Code));

        var query =
            from algorithm in supportedAlgorithms
            from key in supportedKeys
            where algorithm.KeyType.IsInstanceOfType(key)
            select new { key, algorithm };

        throw new NotImplementedException();
    }
}
