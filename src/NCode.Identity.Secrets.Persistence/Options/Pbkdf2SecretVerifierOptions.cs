// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options of the <see cref="SecretVerifierSchemes.Pbkdf2Sha256"/> verifier scheme, which verifies a
/// shared secret someone chose. They are validated as the host starts. A verification of the scheme takes a turn of the
/// <see cref="ISecretVerificationGate"/>, whose limits are <see cref="SecretVerificationGateOptions"/>.
/// </summary>
// See ADR-1391.
[PublicAPI]
public sealed class Pbkdf2SecretVerifierOptions
{
    /// <summary>
    /// Gets the fewest PBKDF2-HMAC-SHA256 iterations a deployment of the server may configure: <c>600000</c>, the
    /// OWASP recommendation for PBKDF2-HMAC-SHA256.
    /// </summary>
    public const int MinimumDeploymentIterations = 600_000;

    /// <summary>
    /// Gets or sets the number of PBKDF2-HMAC-SHA256 iterations of a new verifier. The default is <c>600000</c>, the
    /// OWASP recommendation for PBKDF2-HMAC-SHA256 and the default of the family's password hashes; it must be at least
    /// <c>1</c>, and a deployment of the server may not set fewer than <see cref="MinimumDeploymentIterations"/>.
    /// Each verifier records its own count, so a change applies to secrets stored afterward and every stored verifier
    /// keeps verifying.
    /// </summary>
    public int Iterations { get; set; } = MinimumDeploymentIterations;
}
