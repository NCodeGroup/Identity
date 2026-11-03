// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates <see cref="Pbkdf2SecretVerifierOptions"/> as the host starts, and wherever the options are read: the work
/// factor is at least <c>1</c>.
/// </summary>
// See ADR-1391.
internal sealed class Pbkdf2SecretVerifierOptionsValidator
    : IValidateOptions<Pbkdf2SecretVerifierOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, Pbkdf2SecretVerifierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Iterations < 1
            ? ValidateOptionsResult.Fail(
                $"'{nameof(Pbkdf2SecretVerifierOptions)}.{nameof(Pbkdf2SecretVerifierOptions.Iterations)}' is "
                    + $"{options.Iterations}; it must be at least 1."
            )
            : ValidateOptionsResult.Success;
    }
}
