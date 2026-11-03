// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates <see cref="SecretVerificationGateOptions"/> as the host starts, and wherever the options are read: the
/// concurrency is at least <c>1</c>, the queue length is at least <c>0</c>, and the queue timeout is one a timer
/// accepts. It is the one place these limits are checked.
/// </summary>
// See ADR-1391.
internal sealed class SecretVerificationGateOptionsValidator
    : IValidateOptions<SecretVerificationGateOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SecretVerificationGateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.MaxConcurrency < 1)
        {
            failures.Add(
                $"'{nameof(SecretVerificationGateOptions)}.{nameof(SecretVerificationGateOptions.MaxConcurrency)}' is "
                    + $"{options.MaxConcurrency}; it must be at least 1."
            );
        }

        if (options.MaxQueueLength < 0)
        {
            failures.Add(
                $"'{nameof(SecretVerificationGateOptions)}.{nameof(SecretVerificationGateOptions.MaxQueueLength)}' is "
                    + $"{options.MaxQueueLength}; it must be at least 0."
            );
        }

        if (
            options.QueueTimeout < TimeSpan.FromMilliseconds(1)
            || options.QueueTimeout > TimeSpan.FromMilliseconds(int.MaxValue)
        )
        {
            failures.Add(
                $"'{nameof(SecretVerificationGateOptions)}.{nameof(SecretVerificationGateOptions.QueueTimeout)}' is "
                    + $"{options.QueueTimeout}; it must be from 1 millisecond to {int.MaxValue} milliseconds."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
