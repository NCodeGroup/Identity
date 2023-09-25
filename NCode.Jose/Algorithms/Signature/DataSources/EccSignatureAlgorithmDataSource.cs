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
/// with support for <c>Elliptic-Curve</c> signatures.
/// https://datatracker.ietf.org/doc/html/rfc7518#section-3.4
/// </summary>
public class EccSignatureAlgorithmDataSource : StaticAlgorithmDataSource
{
    /// <inheritdoc />
    public override IEnumerable<IAlgorithm> Algorithms
    {
        get
        {
            // Digital Signature with ECDSA
            // https://datatracker.ietf.org/doc/html/rfc7518#section-3.4

            // ES256
            yield return new EccSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.EcdsaSha256,
                HashAlgorithmName.SHA256);

            // ES384
            yield return new EccSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.EcdsaSha384,
                HashAlgorithmName.SHA384);

            // ES512
            yield return new EccSignatureAlgorithm(
                AlgorithmCodes.DigitalSignature.EcdsaSha512,
                HashAlgorithmName.SHA512);
        }
    }
}
