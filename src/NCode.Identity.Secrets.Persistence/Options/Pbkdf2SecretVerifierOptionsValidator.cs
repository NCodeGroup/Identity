// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates <see cref="Pbkdf2SecretVerifierOptions"/> as the host starts: the work factor and the concurrency are at
/// least <c>1</c>.
/// </summary>
// See ADR-1391.
internal sealed class Pbkdf2SecretVerifierOptionsValidator
    : IValidateOptions<Pbkdf2SecretVerifierOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, Pbkdf2SecretVerifierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.Iterations < 1)
        {
            failures.Add(
                $"'{nameof(Pbkdf2SecretVerifierOptions)}.{nameof(Pbkdf2SecretVerifierOptions.Iterations)}' is "
                    + $"{options.Iterations}; it must be at least 1."
            );
        }

        if (options.MaxConcurrency < 1)
        {
            failures.Add(
                $"'{nameof(Pbkdf2SecretVerifierOptions)}.{nameof(Pbkdf2SecretVerifierOptions.MaxConcurrency)}' is "
                    + $"{options.MaxConcurrency}; it must be at least 1."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
