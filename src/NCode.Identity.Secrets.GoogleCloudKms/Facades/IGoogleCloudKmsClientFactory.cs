// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Google.Cloud.Kms.V1;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.GoogleCloudKms.Facades;

/// <summary>
/// Runs calls with the Google Cloud Key Management Service client, so the code that wraps and unwraps keys can be
/// tested without calling Google Cloud.
/// </summary>
// See ADR-1260 and ADR-1414.
internal interface IGoogleCloudKmsClientFactory
{
    /// <summary>
    /// Runs an operation with the client for the specified access. The same client serves every call with the same
    /// access.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the operation.</typeparam>
    /// <param name="access">
    /// The service account to impersonate, as its email address in <see cref="KeyAccess.Identity"/>; or
    /// <see langword="null"/> to use the credentials of the deployment. <see cref="KeyAccess.Binding"/> is not used.
    /// </param>
    /// <param name="operation">
    /// The operation to run with the <see cref="KeyManagementServiceClient"/> that calls the Cloud KMS endpoint.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The result of the operation.</returns>
    ValueTask<TResult> ExecuteAsync<TResult>(
        KeyAccess? access,
        Func<KeyManagementServiceClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    );
}
