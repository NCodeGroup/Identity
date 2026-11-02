// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Credentials;

/// <summary>
/// Contains the set of cryptographic credentials that are used by <see cref="IJoseSerializer"/> when signing a JWS token.
/// </summary>
/// <param name="SecretKey">The Key Encryption Key (KEK) to use for signing.</param>
/// <param name="SignatureAlgorithm">The <see cref="SignatureAlgorithm"/> to use for signing.</param>
[PublicAPI]
public record JoseSigningCredentials(SecretKey SecretKey, SignatureAlgorithm SignatureAlgorithm)
    : JoseCredentials(SecretKey);
