// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Google.Cloud.Kms.V1;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Facades;

/// <summary>
/// Provides the Google Cloud Key Management Service client, so the code that wraps and unwraps keys can be tested
/// without calling Google Cloud.
/// </summary>
// See ADR-1260.
internal interface IGoogleCloudKmsClientFactory
{
    /// <summary>
    /// Gets the client. The same client is returned for every call.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// operation.</param>
    /// <returns>The <see cref="KeyManagementServiceClient"/> that calls the Cloud KMS endpoint.</returns>
    ValueTask<KeyManagementServiceClient> GetClientAsync(CancellationToken cancellationToken);
}
