// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

/// <summary>
/// Creates the family's verifier schemes for tests, with a low PBKDF2 work factor that keeps them fast; a verifier
/// records its own iteration count.
/// </summary>
internal static class TestSecretVerifierSchemes
{
    public const int Iterations = 1_000;

    public static Pbkdf2SecretVerifierOptions CreateOptions(int iterations = Iterations) =>
        new() { Iterations = iterations };

    public static DefaultSecretVerifierSchemeCatalog CreateCatalog(
        Pbkdf2SecretVerifierOptions? options = null
    ) =>
        new([
            new Sha256SecretVerifierScheme(),
            new Pbkdf2SecretVerifierScheme(
                Microsoft.Extensions.Options.Options.Create(options ?? CreateOptions())
            ),
        ]);
}
