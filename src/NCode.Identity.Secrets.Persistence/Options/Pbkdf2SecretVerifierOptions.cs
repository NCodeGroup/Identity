// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options of the <see cref="SecretVerifierSchemes.Pbkdf2Sha256"/> verifier scheme, which verifies a
/// shared secret someone chose. They are validated as the host starts.
/// </summary>
// See ADR-1391.
[PublicAPI]
public sealed class Pbkdf2SecretVerifierOptions
{
    /// <summary>
    /// Gets the fewest PBKDF2-HMAC-SHA256 iterations a Production host of the server may configure: <c>600000</c>, the
    /// OWASP recommendation for PBKDF2-HMAC-SHA256.
    /// </summary>
    public const int MinimumProductionIterations = 600_000;

    /// <summary>
    /// Gets or sets the number of PBKDF2-HMAC-SHA256 iterations of a new verifier. The default is <c>600000</c>, the
    /// OWASP recommendation for PBKDF2-HMAC-SHA256 and the default of the family's password hashes; it must be at least
    /// <c>1</c>, and a Production host of the server may not set fewer than <see cref="MinimumProductionIterations"/>.
    /// Each verifier records its own count, so a change applies to secrets stored afterward and every stored verifier
    /// keeps verifying.
    /// </summary>
    public int Iterations { get; set; } = 600_000;

    /// <summary>
    /// Gets or sets the most verifications the process runs at once. A verification beyond it waits, without holding
    /// a thread, until one finishes or its request is cancelled, so presented secrets that cost a work factor never
    /// occupy more processors than this however many arrive. The default is half the processors, and at least
    /// <c>1</c>.
    /// </summary>
    public int MaxConcurrency { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);
}
