// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Credentials;

/// <summary>
/// Contains the common set of cryptographic credentials that are used by <see cref="IJoseSerializer"/> when encoding a JOSE token.
/// </summary>
/// <param name="SecretKey">The Key Encryption Key (KEK) to use for encoding.</param>
[PublicAPI]
public abstract record JoseCredentials(SecretKey SecretKey);
