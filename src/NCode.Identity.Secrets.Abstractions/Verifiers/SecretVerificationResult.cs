// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Verifiers;

/// <summary>
/// Describes the outcome of comparing a presented secret with a <see cref="SecretVerifier"/>.
/// </summary>
// See ADR-1391.
[PublicAPI]
public enum SecretVerificationResult
{
    /// <summary>
    /// The presented secret is not the secret the verifier was made from.
    /// </summary>
    Failed = 0,

    /// <summary>
    /// The presented secret is the secret the verifier was made from.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The verifier did not compare the presented secret, because its work factor would have waited longer than the
    /// process allows for a turn. Nothing is known about the presented secret, so it neither authenticates nor counts
    /// as wrong.
    /// </summary>
    Unavailable = 2,
}
