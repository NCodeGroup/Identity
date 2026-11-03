// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Contains the options that choose the registered <see cref="ISecretVerifierScheme"/> instances with which
/// <see cref="ISecretGenerator"/> stores the one-way verifiers of shared secrets that are only ever compared. They are
/// validated as the host starts. A scheme's own parameters are its own options, such as
/// <see cref="Pbkdf2SecretVerifierOptions"/>.
/// </summary>
// See ADR-1391.
[PublicAPI]
public sealed class SharedSecretVerifierOptions
{
    /// <summary>
    /// Gets or sets the name of the registered <see cref="ISecretVerifierScheme"/> that makes the verifier of a shared
    /// secret the server generates with at least 256 bits of entropy. The default is
    /// <see cref="SecretVerifierSchemes.Sha256"/>, because no search can guess such a secret.
    /// </summary>
    public string GeneratedSecretScheme { get; set; } = SecretVerifierSchemes.Sha256;

    /// <summary>
    /// Gets or sets the name of the registered <see cref="ISecretVerifierScheme"/> that makes the verifier of a shared
    /// secret someone chose, whose entropy is unknown. The default is <see cref="SecretVerifierSchemes.Pbkdf2Sha256"/>.
    /// </summary>
    public string SuppliedSecretScheme { get; set; } = SecretVerifierSchemes.Pbkdf2Sha256;
}
