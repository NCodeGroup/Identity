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

using NCode.Jose.Algorithms.DataSources;

namespace NCode.Jose.Algorithms.KeyManagement.DataSources;

/// <summary>
/// Provides an implementation of <see cref="IAlgorithmDataSource"/> that returns the core <c>JOSE</c> algorithm
/// with support for <c>Elliptic-Curve</c> based Key Management.
/// https://datatracker.ietf.org/doc/html/rfc7518#section-4.6
/// </summary>
public class EccKeyManagementAlgorithmDataSource : StaticAlgorithmDataSource
{
    private IAesKeyWrap AesKeyWrap { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EccKeyManagementAlgorithmDataSource"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Provides the common implementation for <c>AES Key Wrap</c> functionality.</param>
    public EccKeyManagementAlgorithmDataSource(IAesKeyWrap aesKeyWrap)
    {
        AesKeyWrap = aesKeyWrap;
    }

    /// <inheritdoc />
    public override IEnumerable<IAlgorithm> Algorithms
    {
        get
        {
            // Key Agreement with Elliptic Curve Diffie-Hellman Ephemeral Static (ECDH-ES)
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.6

            // ECDH-ES
            yield return new EcdhKeyManagementAlgorithm();

            // ECDH-ES+A128KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes128,
                kekSizeBits: 128);

            // ECDH-ES+A192KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes192,
                kekSizeBits: 192);

            // ECDH-ES+A256KW
            yield return new EcdhWithAesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.EcdhEsAes256,
                kekSizeBits: 256);
        }
    }
}
