// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Time;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates the options for the root keys that owners bring when the host starts: every provider that an owner may
/// choose is registered and wraps under a key the caller names, and the validation and retry intervals are within the
/// ranges the validation relies on. Every message names the full configuration key.
/// </summary>
// See ADR-1221 (intervals and retentions are configured and validated when the host starts) and ADR-1411.
internal sealed class OwnerRootKeyOptionsValidator(IEnumerable<IKeyWrapper> keyWrappers)
    : IValidateOptions<SecretProtectionOptions>
{
    internal static readonly TimeSpan MinimumValidationInterval = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan MaximumValidationInterval = TimeSpan.FromDays(1);
    internal static readonly TimeSpan MinimumRetryInterval = TimeSpan.FromSeconds(1);

    private IReadOnlyList<IKeyWrapper> KeyWrappers { get; } = [.. keyWrappers];

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

    private void ValidateProviders(string[]? providers, List<string> failures)
    {
        if (providers is null || providers.Length == 0)
            return;

        var key = Key(nameof(OwnerRootKeyOptions.Providers));
        var referenced = KeyWrappers
            .Where(wrapper => wrapper is IReferencedKeyWrapper)
            .Select(wrapper => wrapper.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        var registered = KeyWrappers
            .Select(wrapper => wrapper.Name)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                failures.Add($"{key} must not contain a blank name.");
                continue;
            }

            if (!seen.Add(provider))
            {
                failures.Add($"{key} names '{provider}' more than once.");
                continue;
            }

            if (referenced.Contains(provider, StringComparer.Ordinal))
                continue;

            var reason = registered.Contains(provider)
                ? "is registered but does not wrap under a key the caller names"
                : "is not registered";

            failures.Add(
                $"{key} names '{provider}', which {reason}. The key wrappers that an owner can choose are: {FormatNames(referenced)}."
            );
        }
    }

    private static string FormatNames(List<string> names) =>
        names.Count == 0 ? "none" : string.Join(", ", names.Select(name => $"'{name}'"));
}
