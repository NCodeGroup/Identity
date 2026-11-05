// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Google.Cloud.Kms.V1;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Facades;

/// <summary>
/// Provides the Google Cloud Key Management Service client, so the code that wraps and unwraps keys can be tested
/// without calling Google Cloud.
/// </summary>
// See ADR-1260 and ADR-1429.
internal interface IGoogleCloudKmsClientFactory
{
    /// <summary>
    /// Gets the client for the specified access. The same client is returned for every call with the same access.
    /// </summary>
    /// <param name="access">
    /// The service account to impersonate, as its email address in <see cref="KeyAccess.Identity"/>; or
    /// <see langword="null"/> to use the credentials of the deployment. <see cref="KeyAccess.Binding"/> is not used.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// operation.</param>
    /// <returns>The <see cref="KeyManagementServiceClient"/> that calls the Cloud KMS endpoint.</returns>
    ValueTask<KeyManagementServiceClient> GetClientAsync(
        KeyAccess? access,
        CancellationToken cancellationToken
    );
}
