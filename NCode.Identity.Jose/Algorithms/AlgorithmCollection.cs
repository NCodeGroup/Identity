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

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides a default implementation for the <see cref="IAlgorithmCollection"/> interface.
/// </summary>
public class AlgorithmCollection : IAlgorithmCollection
{
    private Dictionary<(AlgorithmType type, string code), Algorithm> AlgorithmLookup { get; }

    /// <inheritdoc />
    public int Count => AlgorithmLookup.Count;

    /// <inheritdoc />
    public IEnumerator<Algorithm> GetEnumerator() => AlgorithmLookup.Values.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmCollection"/> class.
    /// </summary>
    public AlgorithmCollection(IEnumerable<Algorithm> items)
    {
        AlgorithmLookup = items.ToDictionary(algorithm => (algorithm.Type, algorithm.Code));
    }

    /// <inheritdoc />
    public bool TryGetAlgorithm<T>(
        AlgorithmType algorithmType,
        string algorithmCode,
        [MaybeNullWhen(false)] out T algorithm)
        where T : Algorithm
    {
        if (AlgorithmLookup.TryGetValue(
                (algorithmType, algorithmCode),
                out var baseAlgorithm) &&
            baseAlgorithm is T typedAlgorithm)
        {
            algorithm = typedAlgorithm;
            return true;
        }

        algorithm = default;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetSignatureAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out SignatureAlgorithm algorithm) =>
        TryGetAlgorithm(AlgorithmType.DigitalSignature, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetKeyManagementAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out KeyManagementAlgorithm algorithm) =>
        TryGetAlgorithm(AlgorithmType.KeyManagement, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetAuthenticatedEncryptionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out AuthenticatedEncryptionAlgorithm algorithm) =>
        TryGetAlgorithm(AlgorithmType.AuthenticatedEncryption, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetCompressionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out CompressionAlgorithm algorithm) =>
        TryGetAlgorithm(AlgorithmType.Compression, algorithmCode, out algorithm);
}
