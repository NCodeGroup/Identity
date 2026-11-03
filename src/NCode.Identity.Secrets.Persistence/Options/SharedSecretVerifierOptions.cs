// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options for the one-way verifiers that <see cref="ISecretGenerator"/> stores for shared secrets that
/// are only ever compared.
/// </summary>
// See ADR-1391.
[PublicAPI]
public sealed class SharedSecretVerifierOptions
{
    /// <summary>
    /// Gets or sets the number of PBKDF2-HMAC-SHA256 iterations of the verifier stored for a shared secret someone
    /// chose. The default is <c>600000</c>, the OWASP recommendation for PBKDF2-HMAC-SHA256 and the default of the
    /// family's password hashes. Each verifier records its own count, so a change applies to secrets stored afterward
    /// and every stored verifier keeps verifying.
    /// </summary>
    public int Pbkdf2Iterations { get; set; } = 600_000;
}
