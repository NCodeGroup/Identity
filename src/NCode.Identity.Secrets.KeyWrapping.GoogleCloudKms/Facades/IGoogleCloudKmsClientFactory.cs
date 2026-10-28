#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Google.Cloud.Kms.V1;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Facades;

/// <summary>
/// Provides the Google Cloud Key Management Service client, so the code that wraps and unwraps keys can be tested
/// without calling Google Cloud.
/// </summary>
// See ADR-0260.
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
