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

namespace NCode.Jose.Algorithms.Signature.DataSources;

/// <summary>
/// Provides an implementation of <see cref="IAlgorithmDataSource"/> that returns the core <c>JOSE</c> algorithms
/// with support for <c>RSASSA-PKCS1-v1_5</c> and <c>RSASSA-PSS</c> signatures.
/// https://datatracker.ietf.org/doc/html/rfc7518#section-3.3
/// https://datatracker.ietf.org/doc/html/rfc7518#section-3.5
/// </summary>
public class RsaSignatureAlgorithmDataSource : StaticAlgorithmDataSource
{
    /// <inheritdoc />
    public override IEnumerable<IAlgorithm> Algorithms
    {
        get
        {
            // Digital Signature with RSASSA-PKCS1-v1_5
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.3

            // RS256
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha256,
                HashAlgorithmName.SHA256,
                SHA256.TryHashData,
                RSASignaturePadding.Pkcs1);

            // RS384
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha384,
                HashAlgorithmName.SHA384,
                SHA384.TryHashData,
                RSASignaturePadding.Pkcs1);

            // RS512
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSha512,
                HashAlgorithmName.SHA512,
                SHA512.TryHashData,
                RSASignaturePadding.Pkcs1);

            // Digital Signature with RSASSA-PSS
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.5

            // PS256
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
                HashAlgorithmName.SHA256,
                SHA256.TryHashData,
                RSASignaturePadding.Pss);

            // PS384
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha384,
                HashAlgorithmName.SHA384,
                SHA384.TryHashData,
                RSASignaturePadding.Pss);

            // PS512
            yield return new RsaSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha512,
                HashAlgorithmName.SHA512,
                SHA512.TryHashData,
                RSASignaturePadding.Pss);
        }
    }
}
