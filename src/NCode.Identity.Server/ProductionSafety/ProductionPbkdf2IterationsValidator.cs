// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Server.ProductionSafety;

/// <summary>
/// Refuses a Production host whose <see cref="Pbkdf2SecretVerifierOptions.Iterations"/> is below
/// <see cref="Pbkdf2SecretVerifierOptions.MinimumProductionIterations"/>, the OWASP recommendation for
/// PBKDF2-HMAC-SHA256. A supplied client secret is stored as a PBKDF2 verifier, and a lower work factor lets whoever
/// holds the root key and the database search it faster.
/// </summary>
// See ADR-1391.
internal sealed class ProductionPbkdf2IterationsValidator
    : IValidateOptions<Pbkdf2SecretVerifierOptions>
{
    /// <summary>
    /// Creates the message that refuses the specified iteration count.
    /// </summary>
    /// <param name="iterations">The configured iteration count.</param>
    /// <returns>The message, which names the configured count and the floor.</returns>
    internal static string CreateMessage(int iterations) =>
        "Refusing to start in the Production environment: "
        + $"'{nameof(Pbkdf2SecretVerifierOptions)}.{nameof(Pbkdf2SecretVerifierOptions.Iterations)}' is "
        + $"{iterations}, below {Pbkdf2SecretVerifierOptions.MinimumProductionIterations}, the OWASP recommendation "
        + "for PBKDF2-HMAC-SHA256. A supplied client secret is stored as a PBKDF2 verifier, and a lower work factor "
        + "lets whoever holds the root key and the database search it faster. Remove the override from this "
        + "deployment.";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, Pbkdf2SecretVerifierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Iterations >= Pbkdf2SecretVerifierOptions.MinimumProductionIterations
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(CreateMessage(options.Iterations));
    }
}
