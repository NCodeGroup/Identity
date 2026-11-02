// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client.Options;

/// <summary>
/// Configures how the OpenID client retrieves, caches and refreshes the metadata and signing keys of an OpenID Provider.
/// </summary>
[PublicAPI]
public sealed class OpenIdClientOptions
{
    /// <summary>
    /// Gets or sets how long retrieved provider metadata and signing keys are served before they are refreshed.
    /// The default is one hour.
    /// </summary>
    public TimeSpan MetadataRefreshInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the minimum time between two refreshes of the same provider, which bounds the traffic that an
    /// unknown signing key can cause. The default is five minutes.
    /// </summary>
    public TimeSpan MinimumRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets how long previously retrieved metadata and signing keys may keep being used when a refresh fails.
    /// The default is 24 hours.
    /// </summary>
    public TimeSpan StaleLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the time one attempt of a request that may be retried — a GET or HEAD without a body or a DPoP
    /// proof, while <see cref="MaxRetryAttempts"/> is above zero — may take until its response headers arrive; a longer
    /// attempt is cancelled and retried. The default is ten seconds. It is between ten milliseconds and five minutes,
    /// and not greater than <see cref="TotalTimeout"/>.
    /// </summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the time a whole HTTP request to an OpenID Provider may take: every attempt, the delays between
    /// retries, and reading the response body. The default is 15 seconds. It is between ten milliseconds and five
    /// minutes.
    /// </summary>
    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Gets or sets how many times a failed attempt is retried; zero turns retries off. Only a GET or HEAD request
    /// without a body or a DPoP proof is retried, after a connection failure, a timed-out attempt, or a 408, 429 or 5xx
    /// response. The default is two. It is between zero and ten.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets the base delay before a retry; it grows exponentially and is jittered. The default is one second.
    /// It is between zero and five minutes.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum size, in bytes, of an HTTP response body that is read from an OpenID Provider.
    /// The default is 524288 (512 KiB).
    /// </summary>
    public int MaxResponseSizeBytes { get; set; } = 524288;

    /// <summary>
    /// Gets or sets the signing algorithms that the client prefers for a <c>private_key_jwt</c> client assertion, most
    /// preferred first, as JOSE <c>alg</c> names. When empty (the default), the client prefers <c>PS256, ES256, PS384,
    /// ES384, PS512, ES512, RS256, RS384, RS512</c>. The list is filtered to what the OpenID Provider advertises in
    /// <c>token_endpoint_auth_signing_alg_values_supported</c>, and an algorithm is only used when the key ring of the
    /// client holds a compatible asymmetric key.
    /// </summary>
    public List<string> ClientAssertionSigningAlgorithms { get; set; } = [];
}
