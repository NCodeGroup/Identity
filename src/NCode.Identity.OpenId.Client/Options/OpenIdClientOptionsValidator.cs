// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Client.Options;

/// <summary>
/// Validates <see cref="OpenIdClientOptions"/> when the host starts, so an interval or limit that cannot work stops
/// the host instead of failing on the first request.
/// </summary>
internal sealed class OpenIdClientOptionsValidator : IValidateOptions<OpenIdClientOptions>
{
    private static TimeSpan MinTimeout { get; } = TimeSpan.FromMilliseconds(10);
    private static TimeSpan MaxTimeout { get; } = TimeSpan.FromMinutes(5);

    private const int MaxRetryAttemptsLimit = 10;
    private const int MinMaxResponseSizeBytes = 1024;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpenIdClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.MetadataRefreshInterval <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.MetadataRefreshInterval)} must be greater than zero; it is {options.MetadataRefreshInterval}."
            );
        }

        if (options.MinimumRefreshInterval <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.MinimumRefreshInterval)} must be greater than zero; it is {options.MinimumRefreshInterval}."
            );
        }

        if (options.StaleLifetime <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.StaleLifetime)} must be greater than zero; it is {options.StaleLifetime}."
            );
        }

        AddHttpFailures(options, failures);

        if (options.StaleLifetime < options.MetadataRefreshInterval)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.StaleLifetime)} must be at least {nameof(OpenIdClientOptions.MetadataRefreshInterval)} ({options.MetadataRefreshInterval}); it is {options.StaleLifetime}."
            );
        }

        if (options.MaxResponseSizeBytes < MinMaxResponseSizeBytes)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.MaxResponseSizeBytes)} must be at least {MinMaxResponseSizeBytes}; it is {options.MaxResponseSizeBytes}."
            );
        }

        var seenAlgorithms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var algorithm in options.ClientAssertionSigningAlgorithms ?? [])
        {
            if (string.IsNullOrWhiteSpace(algorithm))
            {
                failures.Add(
                    $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.ClientAssertionSigningAlgorithms)} must not contain a null, empty or whitespace entry."
                );
            }
            else if (string.Equals(algorithm, "none", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.ClientAssertionSigningAlgorithms)} must not contain 'none'; an unsecured client assertion is never acceptable."
                );
            }
            else if (!seenAlgorithms.Add(algorithm))
            {
                failures.Add(
                    $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.ClientAssertionSigningAlgorithms)} must not contain duplicates; '{algorithm}' appears more than once."
                );
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddHttpFailures(OpenIdClientOptions options, List<string> failures)
    {
        if (options.AttemptTimeout < MinTimeout || options.AttemptTimeout > MaxTimeout)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.AttemptTimeout)} must be between {MinTimeout} and {MaxTimeout}; it is {options.AttemptTimeout}."
            );
        }

        if (options.TotalTimeout < MinTimeout || options.TotalTimeout > MaxTimeout)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.TotalTimeout)} must be between {MinTimeout} and {MaxTimeout}; it is {options.TotalTimeout}."
            );
        }

        if (options.AttemptTimeout > options.TotalTimeout)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.AttemptTimeout)} must not be greater than {nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.TotalTimeout)}; it is {options.AttemptTimeout} and the total is {options.TotalTimeout}."
            );
        }

        if (options.MaxRetryAttempts < 0 || options.MaxRetryAttempts > MaxRetryAttemptsLimit)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.MaxRetryAttempts)} must be between 0 and {MaxRetryAttemptsLimit}; it is {options.MaxRetryAttempts}."
            );
        }

        if (options.RetryDelay < TimeSpan.Zero || options.RetryDelay > MaxTimeout)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.RetryDelay)} must be between {TimeSpan.Zero} and {MaxTimeout}; it is {options.RetryDelay}."
            );
        }
    }
}
