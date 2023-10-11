﻿#region Copyright Preamble

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
using NCode.Jose.Algorithms;
using NCode.Jose.SecretKeys;

namespace NCode.Jose.Credentials;

/// <summary>
/// Provides the ability to retrieve <see cref="JoseSigningCredentials"/> and <see cref="JoseEncryptingCredentials"/> instances
/// based on criteria that specify preferred algorithms and secret keys.
/// </summary>
public interface ICredentialSelector
{
    /// <summary>
    /// Attempts to retrieve <see cref="JoseSigningCredentials"/> based on the specified criteria.
    /// </summary>
    /// <param name="candidateAlgorithms">The composite collection of <see cref="Algorithm"/> instances to consider.</param>
    /// <param name="preferredSignatureAlgorithms">The ordered collection of signature algorithms that are preferred.</param>
    /// <param name="candidateKeys">The ordered composite collection of <see cref="SecretKey"/> instances to consider.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="JoseSigningCredentials"/> that meet the specified criteria.</param>
    /// <returns><c>true</c> if signing credentials were found that match the specified criteria; otherwise, <c>false</c>.</returns>
    bool TryGetSigningCredentials(
        IAlgorithmCollection candidateAlgorithms,
        IEnumerable<string> preferredSignatureAlgorithms,
        IReadOnlyCollection<SecretKey> candidateKeys,
        [MaybeNullWhen(false)] out JoseSigningCredentials credentials);

    /// <summary>
    /// Attempts to retrieve <see cref="JoseEncryptingCredentials"/> based on the specified criteria.
    /// </summary>
    /// <param name="candidateAlgorithms">The composite collection of <see cref="Algorithm"/> instances to consider.</param>
    /// <param name="preferredKeyManagementAlgorithms">The ordered collection of key management algorithms that are preferred.</param>
    /// <param name="preferredEncryptionAlgorithms">The ordered collection of encryption algorithms that are preferred.</param>
    /// <param name="preferredCompressionAlgorithms">The ordered collection of compression algorithms that are preferred.</param>
    /// <param name="candidateKeys">The ordered composite collection of <see cref="SecretKey"/> instances to consider.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="JoseEncryptingCredentials"/> that meet the specified criteria.</param>
    /// <returns><c>true</c> if encryption credentials were found that match the specified criteria; otherwise, <c>false</c>.</returns>
    bool TryGetEncryptingCredentials(
        IAlgorithmCollection candidateAlgorithms,
        IEnumerable<string> preferredKeyManagementAlgorithms,
        IEnumerable<string> preferredEncryptionAlgorithms,
        IEnumerable<string> preferredCompressionAlgorithms,
        IReadOnlyCollection<SecretKey> candidateKeys,
        [MaybeNullWhen(false)] out JoseEncryptingCredentials credentials);
}
