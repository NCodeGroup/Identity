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
using NCode.Jose.Algorithms.AuthenticatedEncryption;
using NCode.Jose.Algorithms.Compression;
using NCode.Jose.Algorithms.KeyManagement;
using NCode.Jose.Algorithms.Signature;

namespace NCode.Jose.Algorithms;

/// <summary>
/// Provides a read-only collection of <see cref="IAlgorithm"/> instances that can be accessed by their purpose.
/// </summary>
public interface IAlgorithmCollection : IReadOnlyCollection<IAlgorithm>
{
    /// <summary>
    /// Gets an <see cref="IAlgorithm"/> that has the specified <paramref name="algorithmType"/> and <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmType">The <see cref="string"/> type of the <see cref="IAlgorithm"/> to get.</param>
    /// <param name="algorithmCode">The <see cref="string"/> code of the <see cref="IAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="IAlgorithm"/> with the specified <paramref name="algorithmType"/> and <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <typeparam name="T">The type of the <see cref="IAlgorithm"/> to get.</typeparam>
    /// <returns><c>true</c> if an <see cref="IAlgorithm"/> with the specified <paramref name="algorithmType"/> and <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetAlgorithm<T>(
        AlgorithmType algorithmType,
        string algorithmCode,
        [MaybeNullWhen(false)] out T algorithm)
        where T : IAlgorithm;

    /// <summary>
    /// Gets an <see cref="ISignatureAlgorithm"/> that has the specified <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmCode">The code of the <see cref="ISignatureAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="ISignatureAlgorithm"/> with the specified <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an <see cref="ISignatureAlgorithm"/> with the specified <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetSignatureAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out ISignatureAlgorithm algorithm);

    /// <summary>
    /// Gets an <see cref="IKeyManagementAlgorithm"/> that has the specified <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmCode">The code of the <see cref="IKeyManagementAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="IKeyManagementAlgorithm"/> with the specified <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an <see cref="IKeyManagementAlgorithm"/> with the specified <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetKeyManagementAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out IKeyManagementAlgorithm algorithm);

    /// <summary>
    /// Gets an <see cref="IAuthenticatedEncryptionAlgorithm"/> that has the specified <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmCode">The code of the <see cref="IAuthenticatedEncryptionAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="IAuthenticatedEncryptionAlgorithm"/> with the specified <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an <see cref="IAuthenticatedEncryptionAlgorithm"/> with the specified <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetAuthenticatedEncryptionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out IAuthenticatedEncryptionAlgorithm algorithm);

    /// <summary>
    /// Gets an <see cref="ICompressionAlgorithm"/> that has the specified <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmCode">The code of the <see cref="ICompressionAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="ICompressionAlgorithm"/> with the specified <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an <see cref="ICompressionAlgorithm"/> with the specified <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetCompressionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out ICompressionAlgorithm algorithm);
}

/// <summary>
/// Provides a default implementation for the <see cref="IAlgorithmCollection"/> interface.
/// </summary>
public class AlgorithmCollection : IAlgorithmCollection
{
    private IReadOnlyDictionary<(AlgorithmType type, string code), IAlgorithm> AlgorithmLookup { get; }

    /// <inheritdoc />
    public int Count => AlgorithmLookup.Count;

    /// <inheritdoc />
    public IEnumerator<IAlgorithm> GetEnumerator() => AlgorithmLookup.Values.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmCollection"/> class.
    /// </summary>
    public AlgorithmCollection(IEnumerable<IAlgorithm> algorithms)
    {
        AlgorithmLookup = algorithms.ToDictionary(algorithm => (algorithm.Type, algorithm.Code));
    }

    /// <inheritdoc />
    public bool TryGetAlgorithm<T>(AlgorithmType algorithmType, string algorithmCode, [MaybeNullWhen(false)] out T algorithm)
        where T : IAlgorithm
    {
        if (AlgorithmLookup.TryGetValue((algorithmType, algorithmCode), out var baseAlgorithm) && baseAlgorithm is T typedAlgorithm)
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
        [MaybeNullWhen(false)] out ISignatureAlgorithm algorithm) =>
        TryGetAlgorithm(AlgorithmType.DigitalSignature, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetKeyManagementAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out IKeyManagementAlgorithm algorithm) =>
        TryGetAlgorithm(AlgorithmType.KeyManagement, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetAuthenticatedEncryptionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out IAuthenticatedEncryptionAlgorithm algorithm) =>
        TryGetAlgorithm(AlgorithmType.AuthenticatedEncryption, algorithmCode, out algorithm);

    /// <inheritdoc />
    public bool TryGetCompressionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out ICompressionAlgorithm algorithm) =>
        TryGetAlgorithm(AlgorithmType.Compression, algorithmCode, out algorithm);
}
