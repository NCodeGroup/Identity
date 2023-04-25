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

using System.Diagnostics.CodeAnalysis;
using NCode.Jose.AuthenticatedEncryption;
using NCode.Jose.KeyManagement;
using NCode.Jose.Signature;

namespace NCode.Jose;

/// <summary>
/// Provides the ability to retrieve <see cref="IAlgorithm"/> instances.
/// </summary>
public interface IAlgorithmProvider
{
    /// <summary>
    /// Gets a collection of all the supported algorithms.
    /// </summary>
    IEnumerable<IAlgorithm> Algorithms { get; }

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
}

/// <summary>
/// Provides a default implementation for the <see cref="IAlgorithmProvider"/> interface.
/// </summary>
public class AlgorithmProvider : IAlgorithmProvider
{
    private IDictionary<(AlgorithmType type, string code), IAlgorithm> AlgorithmLookup { get; }

    /// <inheritdoc />
    public IEnumerable<IAlgorithm> Algorithms => AlgorithmLookup.Values;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlgorithmProvider"/> class.
    /// </summary>
    public AlgorithmProvider(IEnumerable<IAlgorithm> algorithms)
    {
        AlgorithmLookup = algorithms.ToDictionary(algorithm => (algorithm.Type, algorithm.Code));
    }

    private bool TryGetAlgorithm<T>(AlgorithmType type, string code, [MaybeNullWhen(false)] out T algorithm)
        where T : IAlgorithm
    {
        if (AlgorithmLookup.TryGetValue((type, code), out var baseAlgorithm) && baseAlgorithm is T typedAlgorithm)
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
}
