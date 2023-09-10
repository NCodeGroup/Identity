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

using System.Security.Cryptography;
using NCode.Jose.Algorithms.DataSources;

namespace NCode.Jose.Algorithms.KeyManagement.DataSources;

/// <summary>
/// Provides an implementation of <see cref="IAlgorithmDataSource"/> that returns the core <c>JOSE</c> algorithm
/// with support for <c>RSAES-PKCS1-v1_5</c> and <c>RSAES OAEP</c> Key Management.
/// https://datatracker.ietf.org/doc/html/rfc7518#section-4.2
/// https://datatracker.ietf.org/doc/html/rfc7518#section-4.3
/// </summary>
public class RsaKeyManagementAlgorithmDataSource : StaticAlgorithmDataSource
{
    /// <inheritdoc />
    public override IEnumerable<IAlgorithm> Algorithms
    {
        get
        {
            // RSA1_5
            yield return new RsaKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaPkcs1,
                RSAEncryptionPadding.Pkcs1);

            // RSA-OAEP
            yield return new RsaKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep,
                RSAEncryptionPadding.OaepSHA1);

            // RSA-OAEP-256
            yield return new RsaKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep256,
                RSAEncryptionPadding.OaepSHA256);
        }
    }
}
