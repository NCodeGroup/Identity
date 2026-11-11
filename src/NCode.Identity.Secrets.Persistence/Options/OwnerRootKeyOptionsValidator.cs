// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Time;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates the options for the root keys that owners bring when the host starts: every provider that an owner may
/// choose is named once and not blank, and the validation and retry intervals are within the ranges the validation
/// relies on. Every message names the full configuration key. The key wrapper catalog checks that each listed
/// provider is a registered key wrapper that wraps under a key the caller names, because resolving the key wrappers
/// from this validator would close a dependency cycle through the options of the key wrapper adapters.
/// </summary>
// See ADR-1221 (intervals and retentions are configured and validated when the host starts) and ADR-1414.
internal sealed class OwnerRootKeyOptionsValidator : IValidateOptions<SecretProtectionOptions>
{
    internal static TimeSpan MinimumValidationInterval { get; } = TimeSpan.FromMinutes(1);
    internal static TimeSpan MaximumValidationInterval { get; } = TimeSpan.FromDays(1);
    internal static TimeSpan MinimumRetryInterval { get; } = TimeSpan.FromSeconds(1);

    private static string Key(string member) =>
        $"{SecretProtectionOptions.DefaultSectionName}:{nameof(SecretProtectionOptions.OwnerRootKeys)}:{member}";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SecretProtectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rootKeys = options.OwnerRootKeys;
        List<string> failures = [];

        ValidateProviders(rootKeys.Providers, failures);

        var validationIntervalIsValid =
            rootKeys.ValidationInterval >= MinimumValidationInterval
            && rootKeys.ValidationInterval <= MaximumValidationInterval
            && TimerPeriod.IsValid(rootKeys.ValidationInterval);
        if (!validationIntervalIsValid)
        {
            failures.Add(
                $"{Key(nameof(OwnerRootKeyOptions.ValidationInterval))} must be between {MinimumValidationInterval} and {MaximumValidationInterval}; it is {rootKeys.ValidationInterval}."
            );
        }

        if (
            rootKeys.RetryInterval < MinimumRetryInterval
            || rootKeys.RetryInterval > rootKeys.ValidationInterval
            || !TimerPeriod.IsValid(rootKeys.RetryInterval)
        )
        {
            failures.Add(
                $"{Key(nameof(OwnerRootKeyOptions.RetryInterval))} must be between {MinimumRetryInterval} and {Key(nameof(OwnerRootKeyOptions.ValidationInterval))} ({rootKeys.ValidationInterval}); it is {rootKeys.RetryInterval}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateProviders(string[]? providers, List<string> failures)
    {
        if (providers is null || providers.Length == 0)
            return;

        var key = Key(nameof(OwnerRootKeyOptions.Providers));
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                failures.Add($"{key} must not contain a blank name.");
                continue;
            }

            if (!seen.Add(provider))
                failures.Add($"{key} names '{provider}' more than once.");
        }
    }
}
