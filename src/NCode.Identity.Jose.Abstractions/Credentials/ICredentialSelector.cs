// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Credentials;

/// <summary>
/// Provides the ability to retrieve <see cref="JoseSigningCredentials"/> and <see cref="JoseEncryptionCredentials"/> instances
/// based on criteria that specify preferred algorithms and secret keys.
/// </summary>
[PublicAPI]
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
        [MaybeNullWhen(false)] out JoseSigningCredentials credentials
    );

    /// <summary>
    /// Attempts to retrieve <see cref="JoseEncryptionCredentials"/> based on the specified criteria.
    /// </summary>
    /// <param name="candidateAlgorithms">The composite collection of <see cref="Algorithm"/> instances to consider.</param>
    /// <param name="preferredKeyManagementAlgorithms">The ordered collection of key management algorithms that are preferred.</param>
    /// <param name="preferredAuthenticatedEncryptionAlgorithms">The ordered collection of authenticated encryption algorithms that are preferred.</param>
    /// <param name="preferredCompressionAlgorithms">The ordered collection of compression algorithms that are preferred.</param>
    /// <param name="candidateKeys">The ordered composite collection of <see cref="SecretKey"/> instances to consider.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="JoseEncryptionCredentials"/> that meet the specified criteria.</param>
    /// <returns><c>true</c> if encryption credentials were found that match the specified criteria; otherwise, <c>false</c>.</returns>
    bool TryGetEncryptionCredentials(
        IAlgorithmCollection candidateAlgorithms,
        IEnumerable<string> preferredKeyManagementAlgorithms,
        IEnumerable<string> preferredAuthenticatedEncryptionAlgorithms,
        IEnumerable<string> preferredCompressionAlgorithms,
        IReadOnlyCollection<SecretKey> candidateKeys,
        [MaybeNullWhen(false)] out JoseEncryptionCredentials credentials
    );
}
