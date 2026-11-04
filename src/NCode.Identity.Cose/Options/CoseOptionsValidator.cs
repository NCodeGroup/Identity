// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Cose.Options;

/// <summary>
/// Validates <see cref="CoseOptions"/> when the host starts, so a limit that would admit no message stops the host
/// rather than a later read.
/// </summary>
internal class CoseOptionsValidator : IValidateOptions<CoseOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CoseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        // The options are configured in code, by the AddCoseLibrary callback, so a failure names the property and the
        // value it refuses rather than a configuration key.
        Check(failures, nameof(CoseOptions.MaxMessageSizeBytes), options.MaxMessageSizeBytes);
        Check(failures, nameof(CoseOptions.MaxNestingDepth), options.MaxNestingDepth);
        Check(failures, nameof(CoseOptions.MaxSignatures), options.MaxSignatures);
        Check(failures, nameof(CoseOptions.MaxRecipients), options.MaxRecipients);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void Check(List<string> failures, string property, int value)
    {
        if (value < 1)
            failures.Add($"{nameof(CoseOptions)}.{property} must be at least 1; it is {value}.");
    }
}
