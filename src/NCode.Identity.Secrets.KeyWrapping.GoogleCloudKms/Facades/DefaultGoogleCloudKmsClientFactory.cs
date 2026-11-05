// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Kms.V1;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IGoogleCloudKmsClientFactory"/>: one
/// <see cref="KeyManagementServiceClient"/> for the deployment and one for each service account that is impersonated,
/// created on first use and shared by every caller.
/// </summary>
/// <remarks>
/// <para>
/// A client without access resolves Application Default Credentials (the file named by the
/// <c>GOOGLE_APPLICATION_CREDENTIALS</c> environment variable, such as an <c>external_account</c> configuration, or
/// the GKE metadata server) when it is created, so the first call fails when no credentials are found. A failure is
/// not cached, so the next call tries again.
/// </para>
/// <para>
/// A service account that the holder of the key grants through an identity gets its own client, whose credential
/// impersonates the service account with the Application Default Credentials as the source, so the service account
/// must grant the source the Service Account Token Creator role. The SDK refreshes the impersonated tokens. A failure
/// to create the client is not cached either.
/// </para>
/// </remarks>
// See ADR-1260 and ADR-1429.
[ExcludeFromCodeCoverage(
    Justification = "Creates the Google Cloud SDK client; a passthrough to Google.Cloud.Kms.V1."
)]
internal sealed class DefaultGoogleCloudKmsClientFactory : IGoogleCloudKmsClientFactory
{
    private KeyManagementServiceClient? _client;

    private ConcurrentDictionary<string, KeyManagementServiceClient> ImpersonatedClients { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<KeyManagementServiceClient> GetClientAsync(
        KeyAccess? access,
        CancellationToken cancellationToken
    )
    {
        if (access is not null)
        {
            return await GetImpersonatedClientAsync(access, cancellationToken);
        }

        if (Volatile.Read(ref _client) is { } client)
        {
            return client;
        }

        // Two first calls can race; the loser's client is discarded, which is safe because the SDK shares its
        // channels.
        var created = await KeyManagementServiceClient.CreateAsync(cancellationToken);
        return Interlocked.CompareExchange(ref _client, created, null) ?? created;
    }

    private async ValueTask<KeyManagementServiceClient> GetImpersonatedClientAsync(
        KeyAccess access,
        CancellationToken cancellationToken
    )
    {
        if (ImpersonatedClients.TryGetValue(access.Identity, out var client))
        {
            return client;
        }

        var source = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
        var credential = source.Impersonate(
            new ImpersonatedCredential.Initializer(access.Identity)
            {
                Scopes = KeyManagementServiceClient.DefaultScopes,
            }
        );

        // Two first calls can race; the loser's client is discarded, which is safe because the SDK shares its
        // channels.
        var created = await new KeyManagementServiceClientBuilder
        {
            GoogleCredential = credential,
        }.BuildAsync(cancellationToken);
        return ImpersonatedClients.GetOrAdd(access.Identity, created);
    }
}
