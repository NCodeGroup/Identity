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

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides a read-only collection of <see cref="Algorithm"/> instances that can be accessed by their purpose.
/// </summary>
public interface IAlgorithmCollection : IReadOnlyCollection<Algorithm>
{
    /// <summary>
    /// Gets an <see cref="Algorithm"/> that has the specified <paramref name="algorithmType"/> and <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmType">The <see cref="string"/> type of the <see cref="Algorithm"/> to get.</param>
    /// <param name="algorithmCode">The <see cref="string"/> code of the <see cref="Algorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="Algorithm"/> with the specified <paramref name="algorithmType"/> and <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <typeparam name="T">The type of the <see cref="Algorithm"/> to get.</typeparam>
    /// <returns><c>true</c> if an <see cref="Algorithm"/> with the specified <paramref name="algorithmType"/> and <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetAlgorithm<T>(
        AlgorithmType algorithmType,
        string algorithmCode,
        [MaybeNullWhen(false)] out T algorithm)
        where T : Algorithm;

    /// <summary>
    /// Gets an <see cref="SignatureAlgorithm"/> that has the specified <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmCode">The code of the <see cref="SignatureAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="SignatureAlgorithm"/> with the specified <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an <see cref="SignatureAlgorithm"/> with the specified <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetSignatureAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out SignatureAlgorithm algorithm);

    /// <summary>
    /// Gets an <see cref="KeyManagementAlgorithm"/> that has the specified <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmCode">The code of the <see cref="KeyManagementAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="KeyManagementAlgorithm"/> with the specified <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an <see cref="KeyManagementAlgorithm"/> with the specified <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetKeyManagementAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out KeyManagementAlgorithm algorithm);

    /// <summary>
    /// Gets an <see cref="AuthenticatedEncryptionAlgorithm"/> that has the specified <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmCode">The code of the <see cref="AuthenticatedEncryptionAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="AuthenticatedEncryptionAlgorithm"/> with the specified <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an <see cref="AuthenticatedEncryptionAlgorithm"/> with the specified <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetAuthenticatedEncryptionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out AuthenticatedEncryptionAlgorithm algorithm);

    /// <summary>
    /// Gets an <see cref="CompressionAlgorithm"/> that has the specified <paramref name="algorithmCode"/>.
    /// </summary>
    /// <param name="algorithmCode">The code of the <see cref="CompressionAlgorithm"/> to get.</param>
    /// <param name="algorithm">When this method returns, an <see cref="CompressionAlgorithm"/> with the specified <paramref name="algorithmCode"/>, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if an <see cref="CompressionAlgorithm"/> with the specified <paramref name="algorithmCode"/> was found; otherwise, <c>false</c>.</returns>
    bool TryGetCompressionAlgorithm(
        string algorithmCode,
        [MaybeNullWhen(false)] out CompressionAlgorithm algorithm);
}
