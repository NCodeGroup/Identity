// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.KeyManagementService;
using Amazon.Runtime;
using Amazon.Runtime.Credentials;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IAwsKmsClientFactory"/>: one
/// <see cref="AmazonKeyManagementServiceClient"/> for each region and access, created on first use and shared by every
/// caller.
/// </summary>
/// <remarks>
/// <para>
/// A client without access resolves its credentials through the default chain of the AWS SDK (a web identity token, a
/// shared profile, container credentials or instance credentials) on its first request, so creating a client never
/// fails for lack of credentials; the first call that uses one does.
/// </para>
/// <para>
/// A role that the holder of the key grants through an identity gets its own client, whose credentials come from STS
/// AssumeRole with the binding as the external ID. The credentials of the role are cached and refreshed by the SDK,
/// and the credentials that call AssumeRole come from the default chain. A client for a role is confirmed after its
/// first successful call and is then kept until the factory is disposed; a client whose calls have not succeeded is
/// released when a call fails, so that a request for a role that does not work leaves nothing cached, while a failure
/// of a confirmed role (a throttle, for example) leaves its client and credentials in use.
/// </para>
/// <para>
/// Entries are created with <see cref="LazyThreadSafetyMode.PublicationOnly"/>, so a failure to create a client, such
/// as a default credential chain that cannot be resolved, is not cached. Callers that race to create the same entry
/// can each build a client, and only the client that is stored is kept: the others are never stored, so
/// <see cref="Dispose"/> cannot dispose them. A released client is not disposed either. Only the stored clients and
/// their credentials are disposed by <see cref="Dispose"/>.
/// </para>
/// </remarks>
// See ADR-1235.
[ExcludeFromCodeCoverage(
    Justification = "Creates AWS SDK clients; a passthrough to AWSSDK.KeyManagementService."
)]
internal sealed class DefaultAwsKmsClientFactory : IAwsKmsClientFactory, IDisposable
{
    private const string RoleSessionName = "ncode-identity-root-key";

    private ConcurrentDictionary<string, Lazy<ClientEntry>> Clients { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IAmazonKeyManagementService GetClient(string region, KeyAccess? access)
    {
        if (access is null)
        {
            return Clients
                .GetOrAdd(
                    region,
                    static name => new Lazy<ClientEntry>(
                        () =>
                            new ClientEntry(
                                new AmazonKeyManagementServiceClient(
                                    RegionEndpoint.GetBySystemName(name)
                                ),
                                credentials: null
                            ),
                        LazyThreadSafetyMode.PublicationOnly
                    )
                )
                .Value.Client;
        }

        return Clients
            .GetOrAdd(
                GetAccessKey(region, access),
                static (_, state) =>
                    new Lazy<ClientEntry>(
                        () => CreateAssumedRoleClient(state.Region, state.Access),
                        LazyThreadSafetyMode.PublicationOnly
                    ),
                (Region: region, Access: access)
            )
            .Value.Client;
    }

    /// <inheritdoc />
    public void Confirm(string region, KeyAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        if (Clients.TryGetValue(GetAccessKey(region, access), out var lazy) && lazy.IsValueCreated)
        {
            lazy.Value.Confirm();
        }
    }

    /// <inheritdoc />
    public void Release(string region, KeyAccess access)
    {
        ArgumentNullException.ThrowIfNull(access);

        var key = GetAccessKey(region, access);
        if (!Clients.TryGetValue(key, out var lazy))
        {
            return;
        }

        if (lazy.IsValueCreated && lazy.Value.IsConfirmed)
        {
            return;
        }

        // Removes exactly the entry that was read, so an entry that another caller has since added stays cached.
        Clients.TryRemove(new KeyValuePair<string, Lazy<ClientEntry>>(key, lazy));
    }

    private static string GetAccessKey(string region, KeyAccess access) =>
        $"{region}|{access.Identity}|{access.Binding}";

    private static ClientEntry CreateAssumedRoleClient(string region, KeyAccess access)
    {
        var config = new AmazonKeyManagementServiceConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
        };

        var credentials = new AssumeRoleAWSCredentials(
            DefaultAWSCredentialsIdentityResolver.GetCredentials(config),
            access.Identity,
            RoleSessionName,
            new AssumeRoleAWSCredentialsOptions { ExternalId = access.Binding }
        );

        return new ClientEntry(
            new AmazonKeyManagementServiceClient(credentials, config),
            credentials
        );
    }

    /// <summary>
    /// Disposes every client that has been created, with its credentials.
    /// </summary>
    public void Dispose()
    {
        foreach (var entry in Clients.Values)
        {
            if (entry.IsValueCreated)
            {
                entry.Value.Dispose();
            }
        }
    }

    /// <summary>
    /// A client with the credentials that were created for it, if any, so that both are disposed together, and whether a
    /// call through the client has succeeded.
    /// </summary>
    private sealed class ClientEntry(
        AmazonKeyManagementServiceClient client,
        AssumeRoleAWSCredentials? credentials
    ) : IDisposable
    {
        // Read and written with Volatile, because the entry is shared by concurrent calls.
        private bool _confirmed;

        public AmazonKeyManagementServiceClient Client { get; } = client;

        private AssumeRoleAWSCredentials? Credentials { get; } = credentials;

        public bool IsConfirmed => Volatile.Read(ref _confirmed);

        public void Confirm() => Volatile.Write(ref _confirmed, true);

        public void Dispose()
        {
            Client.Dispose();
            (Credentials as IDisposable)?.Dispose();
        }
    }
}
