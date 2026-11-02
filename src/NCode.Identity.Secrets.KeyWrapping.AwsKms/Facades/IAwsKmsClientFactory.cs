// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Amazon.KeyManagementService;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;

/// <summary>
/// Provides the AWS Key Management Service client for a region, so the code that wraps and unwraps keys can be tested
/// without calling AWS.
/// </summary>
// See ADR-1235.
internal interface IAwsKmsClientFactory
{
    /// <summary>
    /// Gets the client for the specified region. The same client is returned for every call with the same region.
    /// </summary>
    /// <param name="region">The system name of the AWS region, such as <c>us-west-2</c>.</param>
    /// <returns>The <see cref="IAmazonKeyManagementService"/> that calls the KMS endpoint of the region.</returns>
    IAmazonKeyManagementService GetClient(string region);
}
