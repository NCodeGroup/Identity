// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Kms.V1;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.GoogleCloudKms.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IGoogleCloudKmsClientFactory"/>: one
/// <see cref="KeyManagementServiceClient"/> for the deployment, created on first use and shared by every caller, and one
/// for each service account that is impersonated, held by a <see cref="KeyAccessClientCache{TClient}"/>.
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
/// to create the client is not cached either. The cache keeps a client for a service account once a call through it
/// succeeds.
/// </para>
/// </remarks>
// See ADR-1260 and ADR-1429.
[ExcludeFromCodeCoverage(
    Justification = "Creates the Google Cloud SDK client; a passthrough to Google.Cloud.Kms.V1."
)]
internal sealed class DefaultGoogleCloudKmsClientFactory(TimeProvider timeProvider)
    : IGoogleCloudKmsClientFactory,
        IDisposable
{
    private static TimeSpan ImpersonatedIdleTimeout { get; } = TimeSpan.FromDays(1);

    private KeyManagementServiceClient? _client;

    private KeyAccessClientCache<KeyManagementServiceClient> ImpersonatedClients { get; } =
        new(timeProvider, ImpersonatedIdleTimeout);

    /// <inheritdoc />
    public async ValueTask<TResult> ExecuteAsync<TResult>(
        KeyAccess? access,
        Func<KeyManagementServiceClient, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (access is null)
        {
            var client = await GetDeploymentClientAsync(cancellationToken);
            return await operation(client, cancellationToken);
        }

        return await ImpersonatedClients.UseAsync(
            access.Identity,
            token => CreateImpersonatedClientAsync(access, token),
            operation,
            cancellationToken
        );
    }

    private async ValueTask<KeyManagementServiceClient> GetDeploymentClientAsync(
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

    private static async ValueTask<KeyManagementServiceClient> CreateImpersonatedClientAsync(
        KeyAccess access,
        CancellationToken cancellationToken
    )
    {
        var source = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
        var credential = source.Impersonate(
            new ImpersonatedCredential.Initializer(access.Identity)
            {
                Scopes = KeyManagementServiceClient.DefaultScopes,
            }
        );

        return await new KeyManagementServiceClientBuilder
        {
            GoogleCredential = credential,
        }.BuildAsync(cancellationToken);
    }

    /// <summary>
    /// Releases the cached clients of the impersonated service accounts.
    /// </summary>
    public void Dispose() => ImpersonatedClients.Dispose();
}
