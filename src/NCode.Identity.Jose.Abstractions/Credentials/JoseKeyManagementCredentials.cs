// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Credentials;

/// <summary>
/// Contains the per-recipient cryptographic credentials that are used by <see cref="IJoseSerializer"/> when
/// encrypting a JWE token using the JSON serialization. The content-encryption (<c>enc</c>) and compression
/// (<c>zip</c>) algorithms are shared across all recipients and are therefore specified separately.
/// </summary>
/// <param name="SecretKey">The Key Encryption Key (KEK) to use for this recipient.</param>
/// <param name="KeyManagementAlgorithm">The <see cref="KeyManagementAlgorithm"/> to use for this recipient.</param>
[PublicAPI]
public record JoseKeyManagementCredentials(
    SecretKey SecretKey,
    KeyManagementAlgorithm KeyManagementAlgorithm
) : JoseCredentials(SecretKey);
