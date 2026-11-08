// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Google.Api.Gax.Grpc;
using Grpc.Core;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms;

/// <summary>
/// Contains the <see cref="CallSettings"/> of the calls to Google Cloud KMS, so that the retry of a failed call, with
/// jittered back-off, is done by the client library in one layer and with the same settings for every call.
/// </summary>
// See ADR-1260.
internal static class GoogleCloudKmsCallSettings
{
    private const int RetryingMaxAttempts = 4;
    private const int CreatingMaxAttempts = 3;
    private const double BackoffMultiplier = 2.0;

    private static readonly TimeSpan InitialBackoff = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Gets the settings of a call that is safe to repeat, such as signing, reading a public key or reading a crypto
    /// key version. The call is retried when Cloud KMS answers <see cref="StatusCode.ResourceExhausted"/>,
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
    /// Gets the settings of a call that creates or destroys a crypto key or a crypto key version. The call is retried
    /// only when Cloud KMS answers <see cref="StatusCode.ResourceExhausted"/>, which says the request was not
    /// processed.
    /// </summary>
    public static CallSettings Creating { get; } =
        CallSettings.FromRetry(
            RetrySettings.FromExponentialBackoff(
                maxAttempts: CreatingMaxAttempts,
                initialBackoff: InitialBackoff,
                maxBackoff: MaxBackoff,
                backoffMultiplier: BackoffMultiplier,
                retryFilter: RetrySettings.FilterForStatusCodes(StatusCode.ResourceExhausted),
                backoffJitter: RetrySettings.RandomJitter
            )
        );
}
