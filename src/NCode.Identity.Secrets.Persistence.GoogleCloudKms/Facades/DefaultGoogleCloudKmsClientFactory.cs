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

using System.Diagnostics.CodeAnalysis;
using Google.Cloud.Kms.V1;

namespace NCode.Identity.Secrets.Persistence.GoogleCloudKms.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IGoogleCloudKmsClientFactory"/>: one
/// <see cref="KeyManagementServiceClient"/>, created on first use and shared by every caller.
/// </summary>
/// <remarks>
/// A client resolves Application Default Credentials (the file named by the <c>GOOGLE_APPLICATION_CREDENTIALS</c>
/// environment variable, such as an <c>external_account</c> configuration, or the GKE metadata server) when it is
/// created, so the first call fails when no credentials are found. A failure is not cached, so the next call tries
/// again.
/// </remarks>
// See ADR-0238.
[ExcludeFromCodeCoverage(
    Justification = "Creates the Google Cloud SDK client; a passthrough to Google.Cloud.Kms.V1."
)]
internal sealed class DefaultGoogleCloudKmsClientFactory : IGoogleCloudKmsClientFactory
{
    private KeyManagementServiceClient? _client;

    /// <inheritdoc />
    public async ValueTask<KeyManagementServiceClient> GetClientAsync(
        CancellationToken cancellationToken
    )
    {
        if (Volatile.Read(ref _client) is { } client)
        {
            return client;
        }

        // Two first calls can race; the loser's client is discarded, which is safe because the SDK shares its
        // channels.
        var created = await KeyManagementServiceClient.CreateAsync(cancellationToken);
        return Interlocked.CompareExchange(ref _client, created, null) ?? created;
    }
}
