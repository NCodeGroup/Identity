// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Credentials;

/// <summary>
/// Contains the set of cryptographic credentials that are used by <see cref="IJoseSerializer"/> when encrypting a JWE token.
/// </summary>
/// <param name="SecretKey">The Key Encryption Key (KEK) to use for encryption.</param>
/// <param name="KeyManagementAlgorithm">The <see cref="KeyManagementAlgorithm"/> to use for key management.</param>
/// <param name="AuthenticatedEncryptionAlgorithm">The <see cref="AuthenticatedEncryptionAlgorithm"/> to use for authenticated encryption.</param>
/// <param name="CompressionAlgorithm">The optional <see cref="CompressionAlgorithm"/> to use for compression.</param>
[PublicAPI]
public record JoseEncryptionCredentials(
    SecretKey SecretKey,
    KeyManagementAlgorithm KeyManagementAlgorithm,
    AuthenticatedEncryptionAlgorithm AuthenticatedEncryptionAlgorithm,
    CompressionAlgorithm? CompressionAlgorithm = null
) : JoseCredentials(SecretKey);
