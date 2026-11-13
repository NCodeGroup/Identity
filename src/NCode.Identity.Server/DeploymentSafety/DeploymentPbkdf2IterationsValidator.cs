// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Server.DeploymentSafety;

/// <summary>
/// Refuses a deployment (a host without <c>LocalDevelopment:Enabled</c>) whose
/// <see cref="Pbkdf2SecretVerifierOptions.Iterations"/> is below
/// <see cref="Pbkdf2SecretVerifierOptions.MinimumDeploymentIterations"/>, the OWASP recommendation for
/// PBKDF2-HMAC-SHA256. A supplied client secret is stored as a PBKDF2 verifier, and a lower work factor lets whoever
/// holds the root key and the database search it faster.
/// </summary>
// See ADR-1391.
internal sealed class DeploymentPbkdf2IterationsValidator
    : IValidateOptions<Pbkdf2SecretVerifierOptions>
{
    /// <summary>
    /// Creates the message that refuses the specified iteration count.
    /// </summary>
    /// <param name="iterations">The configured iteration count.</param>
    /// <returns>The message, which names the configured count and the floor.</returns>
    internal static string CreateMessage(int iterations) =>
        "Refusing to start a deployment: "
        + $"'{nameof(Pbkdf2SecretVerifierOptions)}.{nameof(Pbkdf2SecretVerifierOptions.Iterations)}' is "
        + $"{iterations}, below {Pbkdf2SecretVerifierOptions.MinimumDeploymentIterations}, the OWASP recommendation "
        + "for PBKDF2-HMAC-SHA256. A supplied client secret is stored as a PBKDF2 verifier, and a lower work factor "
        + "lets whoever holds the root key and the database search it faster. Remove the override from this "
        + "deployment. A host for local development sets 'LocalDevelopment:Enabled' to true.";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, Pbkdf2SecretVerifierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Iterations >= Pbkdf2SecretVerifierOptions.MinimumDeploymentIterations
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(CreateMessage(options.Iterations));
    }
}
