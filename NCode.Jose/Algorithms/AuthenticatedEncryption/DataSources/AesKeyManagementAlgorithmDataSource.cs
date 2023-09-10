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

namespace NCode.Jose.Algorithms.AuthenticatedEncryption.DataSources;

/// <summary>
/// Provides an implementation of <see cref="IAlgorithmDataSource"/> that returns the core <c>JOSE</c> algorithm
/// with support for <c>AES</c> based content encryption.
/// https://datatracker.ietf.org/doc/html/rfc7518#section-5.2
/// https://datatracker.ietf.org/doc/html/rfc7518#section-5.3
/// </summary>
public class AesAuthenticatedEncryptionAlgorithmDataSource : StaticAlgorithmDataSource
{
    /// <inheritdoc />
    public override IEnumerable<IAlgorithm> Algorithms
    {
        get
        {
            // AES_CBC_HMAC_SHA2 Algorithms
            // https://datatracker.ietf.org/doc/html/rfc7518#section-5.2

            // A128CBC-HS256
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256,
                HMACSHA256.TryHashData,
                cekSizeBits: 256);

            // A192CBC-HS384
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes192CbcHmacSha384,
                HMACSHA384.TryHashData,
                cekSizeBits: 384);

            // A256CBC-HS512
            yield return new AesCbcHmacAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256CbcHmacSha512,
                HMACSHA512.TryHashData,
                cekSizeBits: 512);

            // Content Encryption with AES GCM
            // https://datatracker.ietf.org/doc/html/rfc7518#section-5.3

            // A128GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes128Gcm,
                cekSizeBits: 128);

            // A192GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes192Gcm,
                cekSizeBits: 192);

            // A256GCM
            yield return new AesGcmAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm,
                cekSizeBits: 256);
        }
    }
}
