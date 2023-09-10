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
/// with support for <c>password-based encryption</c> Key Management.
/// https://datatracker.ietf.org/doc/html/rfc7518#section-4.8
/// </summary>
public class Pbes2KeyManagementAlgorithmDataSource : StaticAlgorithmDataSource
{
    private IAesKeyWrap AesKeyWrap { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Pbes2KeyManagementAlgorithmDataSource"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Provides the common implementation for <c>AES Key Wrap</c> functionality.</param>
    public Pbes2KeyManagementAlgorithmDataSource(IAesKeyWrap aesKeyWrap)
    {
        AesKeyWrap = aesKeyWrap;
    }

    /// <inheritdoc />
    public override IEnumerable<IAlgorithm> Algorithms
    {
        get
        {
            // PBES2-HS256+A128KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha256Aes128,
                HashAlgorithmName.SHA256,
                keySizeBits: 128,
                maxIterationCount: 310000);

            // PBES2-HS384+A192KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha384Aes192,
                HashAlgorithmName.SHA384,
                keySizeBits: 192,
                maxIterationCount: 250000);

            // PBES2-HS512+A256KW
            yield return new Pbes2KeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Pbes2HmacSha512Aes256,
                HashAlgorithmName.SHA512,
                keySizeBits: 256,
                maxIterationCount: 120000);
        }
    }
}
