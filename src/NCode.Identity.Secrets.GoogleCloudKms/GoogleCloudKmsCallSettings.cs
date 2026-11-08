// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Google.Api.Gax.Grpc;
using Grpc.Core;

namespace NCode.Identity.Secrets.GoogleCloudKms;

/// <summary>
/// Contains the <see cref="CallSettings"/> of the calls to Google Cloud KMS: a call that is safe to repeat is retried,
/// with jittered back-off, by the client library in one layer and with the same settings for every such call, and a
/// call that creates a crypto key or a crypto key version is made once.
/// </summary>
// See ADR-1471.
internal static class GoogleCloudKmsCallSettings
{
    private const int RetryingMaxAttempts = 4;
    private const double BackoffMultiplier = 2.0;

    private static TimeSpan InitialBackoff { get; } = TimeSpan.FromMilliseconds(200);
    private static TimeSpan MaxBackoff { get; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets the settings of a call that is safe to repeat, such as signing, reading a public key, reading a crypto
    /// key version or destroying a crypto key version. The call is retried when Cloud KMS answers <see cref="StatusCode.ResourceExhausted"/>,
    /// <see cref="StatusCode.Unavailable"/> or <see cref="StatusCode.DeadlineExceeded"/>.
    /// </summary>
    public static CallSettings Retrying { get; } =
        CallSettings.FromRetry(
            RetrySettings.FromExponentialBackoff(
                maxAttempts: RetryingMaxAttempts,
                initialBackoff: InitialBackoff,
                maxBackoff: MaxBackoff,
                backoffMultiplier: BackoffMultiplier,
                retryFilter: RetrySettings.FilterForStatusCodes(
                    StatusCode.ResourceExhausted,
                    StatusCode.Unavailable,
                    StatusCode.DeadlineExceeded
                ),
                backoffJitter: RetrySettings.RandomJitter
            )
        );

    /// <summary>
    /// Gets the settings of a call that creates a crypto key or a crypto key version. A call that creates a crypto key
    /// or version is made once, because a repeated call can create a second one that nothing records.
    /// </summary>
    public static CallSettings Creating { get; } =
        // An explicit retry setting overrides any retry the client's defaults give the method; one attempt uses no back-off.
        CallSettings.FromRetry(
            RetrySettings.FromExponentialBackoff(
                maxAttempts: 1,
                initialBackoff: TimeSpan.Zero,
                maxBackoff: TimeSpan.Zero,
                backoffMultiplier: 1.0,
                retryFilter: static _ => false,
                backoffJitter: RetrySettings.NoJitter
            )
        );
}
