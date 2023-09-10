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
/// with support for <c>AES</c> based Key Management.
/// https://datatracker.ietf.org/doc/html/rfc7518#section-4.4
/// https://datatracker.ietf.org/doc/html/rfc7518#section-4.7
/// </summary>
public class AesKeyManagementAlgorithmDataSource : StaticAlgorithmDataSource
{
    private IAesKeyWrap AesKeyWrap { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AesKeyManagementAlgorithmDataSource"/> class.
    /// </summary>
    /// <param name="aesKeyWrap">Provides the common implementation for <c>AES Key Wrap</c> functionality.</param>
    public AesKeyManagementAlgorithmDataSource(IAesKeyWrap aesKeyWrap)
    {
        AesKeyWrap = aesKeyWrap;
    }

    /// <inheritdoc />
    public override IEnumerable<IAlgorithm> Algorithms
    {
        get
        {
            // AES Key Wrap
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.4

            // A128KW
            yield return new AesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Aes128,
                kekSizeBits: 128);

            // A192KW
            yield return new AesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Aes192,
                kekSizeBits: 192);

            // A256KW
            yield return new AesKeyManagementAlgorithm(
                AesKeyWrap,
                AlgorithmCodes.KeyManagement.Aes256,
                kekSizeBits: 256);

            // AES GCM
            // https://datatracker.ietf.org/doc/html/rfc7518#section-4.7

            // A128GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes128Gcm,
                kekSizeBits: 128);

            // A192GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes192Gcm,
                kekSizeBits: 192);

            // A256GCMKW
            yield return new AesGcmKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.Aes256Gcm,
                kekSizeBits: 256);
        }
    }
}
