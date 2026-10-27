#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Collections.Providers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa.KeyTypes;

namespace NCode.Identity.Jose.EdDsa.Algorithms;

/// <summary>
/// Contributes the <c>EdDSA</c>, <c>Ed25519</c> and <c>Ed448</c> <see cref="SignatureAlgorithm"/> instances to the
/// <c>JOSE</c> algorithm collection.
/// </summary>
internal sealed class EdDsaAlgorithmDataSource : ICollectionDataSource<Algorithm>
{
    private static readonly Algorithm[] Algorithms =
    [
        new EdDsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.EdDsa, curve: null),
        new EdDsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.Ed25519, OkpCurves.Ed25519),
        new EdDsaSignatureAlgorithm(AlgorithmCodes.DigitalSignature.Ed448, OkpCurves.Ed448),
    ];

    /// <inheritdoc />
    public IEnumerable<Algorithm> Collection => Algorithms;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
}
