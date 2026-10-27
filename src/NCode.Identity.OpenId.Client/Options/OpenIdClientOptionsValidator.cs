#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Client.Options;

/// <summary>
/// Validates <see cref="OpenIdClientOptions"/> when the host starts, so an interval or limit that cannot work stops
/// the host instead of failing on the first request.
/// </summary>
internal sealed class OpenIdClientOptionsValidator : IValidateOptions<OpenIdClientOptions>
{
    private static readonly TimeSpan MaxRequestTimeout = TimeSpan.FromMinutes(5);

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

        if (options.RequestTimeout <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.RequestTimeout)} must be greater than zero; it is {options.RequestTimeout}."
            );
        }
        else if (options.RequestTimeout > MaxRequestTimeout)
        {
            failures.Add(
                $"{nameof(OpenIdClientOptions)}.{nameof(OpenIdClientOptions.RequestTimeout)} must be at most {MaxRequestTimeout}; it is {options.RequestTimeout}."
            );
        }

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

        ValidateAlgorithms(
            nameof(OpenIdClientOptions.ClientAssertionSigningAlgorithms),
            options.ClientAssertionSigningAlgorithms,
            failures
        );
        ValidateAlgorithms(
            nameof(OpenIdClientOptions.IdTokenSigningAlgorithms),
            options.IdTokenSigningAlgorithms,
            failures
        );

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateAlgorithms(
        string propertyName,
        IEnumerable<string>? algorithms,
        List<string> failures
    )
    {
        var seenAlgorithms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var algorithm in algorithms ?? [])
        {
            if (string.IsNullOrWhiteSpace(algorithm))
            {
                failures.Add(
                    $"{nameof(OpenIdClientOptions)}.{propertyName} must not contain a null, empty or whitespace entry."
                );
            }
            else if (string.Equals(algorithm, "none", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{nameof(OpenIdClientOptions)}.{propertyName} must not contain 'none'; an unsecured token is never acceptable."
                );
            }
            else if (!seenAlgorithms.Add(algorithm))
            {
                failures.Add(
                    $"{nameof(OpenIdClientOptions)}.{propertyName} must not contain duplicates; '{algorithm}' appears more than once."
                );
            }
        }
    }
}
