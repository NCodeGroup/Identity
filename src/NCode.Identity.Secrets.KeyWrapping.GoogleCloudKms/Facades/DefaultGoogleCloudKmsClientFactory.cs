// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Google.Cloud.Kms.V1;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Facades;

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
// See ADR-1260.
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
