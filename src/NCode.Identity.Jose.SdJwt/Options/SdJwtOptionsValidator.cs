// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.Jose.SdJwt.Options;

/// <summary>
/// Validates <see cref="SdJwtOptions"/> when the host starts, so a limit that would weaken a salt or admit an
/// unbounded presentation stops the host rather than a later issuance or verification.
/// </summary>
internal class SdJwtOptionsValidator : IValidateOptions<SdJwtOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SdJwtOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.SaltSizeBytes < SdJwtOptions.MinimumSaltSizeBytes)
            failures.Add(
                $"{nameof(SdJwtOptions.SaltSizeBytes)} must be at least {SdJwtOptions.MinimumSaltSizeBytes}."
            );

        if (options.MaxDisclosureCount < 1)
            failures.Add($"{nameof(SdJwtOptions.MaxDisclosureCount)} must be at least 1.");

        if (options.MaxNestingDepth < 1)
            failures.Add($"{nameof(SdJwtOptions.MaxNestingDepth)} must be at least 1.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
