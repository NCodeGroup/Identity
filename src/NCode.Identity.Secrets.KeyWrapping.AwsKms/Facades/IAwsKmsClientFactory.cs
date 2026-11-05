// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Amazon.KeyManagementService;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;

/// <summary>
/// Runs calls with the AWS Key Management Service client for a region, so the code that wraps and unwraps keys can be
/// tested without calling AWS.
/// </summary>
// See ADR-1235.
internal interface IAwsKmsClientFactory
{
    /// <summary>
    /// Runs an operation with the client for the specified region and access. The same client serves every call with
    /// the same region and access.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the operation.</typeparam>
    /// <param name="region">The system name of the AWS region, such as <c>us-west-2</c>.</param>
    /// <param name="access">
    /// The IAM role to assume, as its ARN in <see cref="KeyAccess.Identity"/> and the external ID in
    /// <see cref="KeyAccess.Binding"/>; or <see langword="null"/> to use the credentials of the deployment.
    /// </param>
    /// <param name="operation">
    /// The operation to run with the <see cref="IAmazonKeyManagementService"/> that calls the KMS endpoint of the
    /// region.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    ValueTask<TResult> ExecuteAsync<TResult>(
        string region,
        KeyAccess? access,
        Func<IAmazonKeyManagementService, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    );
}
