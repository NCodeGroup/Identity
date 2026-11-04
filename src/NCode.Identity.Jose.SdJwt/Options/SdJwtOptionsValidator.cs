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

        // The options are configured in code, by the AddSdJwt callback, so a failure names the property and the
        // value it refuses rather than a configuration key.
        if (options.SaltSizeBytes < SdJwtOptions.MinimumSaltSizeBytes)
            failures.Add(
                $"{nameof(SdJwtOptions)}.{nameof(SdJwtOptions.SaltSizeBytes)} must be at least {SdJwtOptions.MinimumSaltSizeBytes}; it is {options.SaltSizeBytes}."
            );

        if (options.MaxDisclosureCount < 1)
            failures.Add(
                $"{nameof(SdJwtOptions)}.{nameof(SdJwtOptions.MaxDisclosureCount)} must be at least 1; it is {options.MaxDisclosureCount}."
            );

        if (options.MaxNestingDepth < 1)
            failures.Add(
                $"{nameof(SdJwtOptions)}.{nameof(SdJwtOptions.MaxNestingDepth)} must be at least 1; it is {options.MaxNestingDepth}."
            );

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
