// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Azure.Core;

namespace NCode.Identity.Secrets.AzureKeyVault.Facades;

/// <summary>
/// Configures the retry of every Azure Key Vault client that the package creates: the SDK retries a transient failure
/// up to three times with an exponential back-off, no other layer retries it, and a key creation is not retried.
/// </summary>
// See ADR-1471.
internal static class AzureKeyVaultClientOptions
{
    /// <summary>
    /// The number of times a failed request is retried.
    /// </summary>
    public const int MaxRetries = 3;

    /// <summary>
    /// The delay before the first retry; each later retry waits longer.
    /// </summary>
    public static TimeSpan Delay { get; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// The longest delay between two retries.
    /// </summary>
    public static TimeSpan MaxDelay { get; } = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Sets the retry of the options of a client to an exponential back-off.
    /// </summary>
    /// <typeparam name="T">The type of the options of the client.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <returns>The same <paramref name="options"/>.</returns>
    public static T Configure<T>(T options)
        where T : ClientOptions
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Retry.Mode = RetryMode.Exponential;
        options.Retry.MaxRetries = MaxRetries;
        options.Retry.Delay = Delay;
        options.Retry.MaxDelay = MaxDelay;

        return options;
    }

    /// <summary>
    /// Sets the options of a client to never retry a request, for a call that creates a key and so must not run twice.
    /// </summary>
    /// <typeparam name="T">The type of the options of the client.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <returns>The same <paramref name="options"/>.</returns>
    public static T ConfigureWithoutRetry<T>(T options)
        where T : ClientOptions
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Retry.MaxRetries = 0;

        return options;
    }
}
