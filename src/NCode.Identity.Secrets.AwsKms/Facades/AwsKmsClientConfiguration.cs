// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;

namespace NCode.Identity.Secrets.AwsKms.Facades;

/// <summary>
/// Creates the configuration of every <see cref="AmazonKeyManagementServiceClient"/>, so that the retry of a failed
/// call, with jittered back-off, is done by the SDK in one layer and with the same settings for every client.
/// </summary>
// See ADR-1471.
internal static class AwsKmsClientConfiguration
{
    /// <summary>
    /// The maximum number of times the SDK retries a failed call.
    /// </summary>
    public const int MaxErrorRetry = 3;

    /// <summary>
    /// Creates the configuration of a client that calls AWS Key Management Service in a region.
    /// </summary>
    /// <param name="region">The system name of the AWS region, such as <c>us-west-2</c>.</param>
    /// <returns>The configuration of the client, with the standard retry mode of the SDK.</returns>
    public static AmazonKeyManagementServiceConfig Create(string region) =>
        new()
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            RetryMode = RequestRetryMode.Standard,
            MaxErrorRetry = MaxErrorRetry,
        };
}
