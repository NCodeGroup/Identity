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
    /// Gets the client for the specified region and access. The same client is returned for every call with the same
    /// region and access.
    /// </summary>
    /// <param name="region">The system name of the AWS region, such as <c>us-west-2</c>.</param>
    /// <param name="access">
    /// The IAM role to assume, as its ARN in <see cref="KeyAccess.Identity"/> and the external ID in
    /// <see cref="KeyAccess.Binding"/>; or <see langword="null"/> to use the credentials of the deployment.
    /// </param>
    /// <returns>The <see cref="IAmazonKeyManagementService"/> that calls the KMS endpoint of the region.</returns>
    IAmazonKeyManagementService GetClient(string region, KeyAccess? access);

    /// <summary>
    /// Marks the client for the specified region and access as confirmed, because a call through it succeeded. A
    /// confirmed client is kept until the factory is disposed, and <see cref="Release"/> leaves it cached. Does nothing
    /// when no such client exists.
    /// </summary>
    /// <param name="region">The system name of the AWS region, such as <c>us-west-2</c>.</param>
    /// <param name="access">The IAM role and external ID that the client to confirm was created for.</param>
    void Confirm(string region, KeyAccess access);

    /// <summary>
    /// Removes the client for the specified region and access from the cache when no call through it has succeeded, so
    /// that an access that never worked leaves nothing cached. A confirmed client stays cached, and a removed client is
    /// not disposed. Does nothing when no such client exists.
    /// </summary>
    /// <param name="region">The system name of the AWS region, such as <c>us-west-2</c>.</param>
    /// <param name="access">The IAM role and external ID that the client to release was created for.</param>
    void Release(string region, KeyAccess access);
}
