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
/// to create the client is not cached either. A client is confirmed after its first successful call and is then kept
/// for the lifetime of the factory; a client whose calls have not succeeded is removed when the wrapper releases it
/// after a failed call, so the next call creates it again, while a failure of a confirmed service account (a throttle,
/// for example) leaves its client in use.
/// </para>
/// </remarks>
// See ADR-1260 and ADR-1429.
[ExcludeFromCodeCoverage(
    Justification = "Creates the Google Cloud SDK client; a passthrough to Google.Cloud.Kms.V1."
)]
internal sealed class DefaultGoogleCloudKmsClientFactory : IGoogleCloudKmsClientFactory
{
    private KeyManagementServiceClient? _client;

    private ConcurrentDictionary<string, ClientEntry> ImpersonatedClients { get; } =
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
        if (ImpersonatedClients.TryGetValue(access.Identity, out var entry))
        {
            return entry.Client;
        }

        var source = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
        var credential = source.Impersonate(
            new ImpersonatedCredential.Initializer(access.Identity)
            {
                Scopes = KeyManagementServiceClient.DefaultScopes,
            }
        );

        // Two first calls can race; the loser's client is discarded. A client built with its own credential has its
        // own channel, which nothing else references, so a discarded client is reclaimed with its channel.
        var created = await new KeyManagementServiceClientBuilder
        {
            GoogleCredential = credential,
        }.BuildAsync(cancellationToken);
        return ImpersonatedClients.GetOrAdd(access.Identity, new ClientEntry(created)).Client;
    }

    /// <inheritdoc />
    public void Confirm(KeyAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        if (ImpersonatedClients.TryGetValue(access.Identity, out var entry))
        {
            entry.Confirm();
        }
    }

    /// <inheritdoc />
    public void Release(KeyAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        if (!ImpersonatedClients.TryGetValue(access.Identity, out var entry) || entry.IsConfirmed)
        {
            return;
        }

        // Removes exactly the entry that was read, so an entry that another caller has since added stays cached. A
        // released client has its own channel, which nothing else references, so it is reclaimed with its channel.
        ImpersonatedClients.TryRemove(
            new KeyValuePair<string, ClientEntry>(access.Identity, entry)
        );
    }

    /// <summary>
    /// A client for an impersonated service account and whether a call through it has succeeded.
    /// </summary>
    private sealed class ClientEntry(KeyManagementServiceClient client)
    {
        // Read and written with Volatile, because the entry is shared by concurrent calls.
        private bool _confirmed;

        public KeyManagementServiceClient Client { get; } = client;

        public bool IsConfirmed => Volatile.Read(ref _confirmed);

        public void Confirm() => Volatile.Write(ref _confirmed, true);
    }
}
