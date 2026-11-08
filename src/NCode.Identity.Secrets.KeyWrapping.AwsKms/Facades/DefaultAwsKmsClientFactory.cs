// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Amazon.KeyManagementService;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IAwsKmsClientFactory"/>: one
/// <see cref="AmazonKeyManagementServiceClient"/> for each region, created on first use and shared by every caller, and
/// one client for each region and access, held by a <see cref="KeyAccessClientCache{TClient}"/>.
/// </summary>
/// <remarks>
/// <para>
/// A client without access resolves its credentials through the default chain of the AWS SDK (a web identity token, a
/// shared profile, container credentials or instance credentials) on its first request, so creating a client never
/// fails for lack of credentials; the first call that uses one does.
/// </para>
/// <para>
/// A role that the holder of the key grants through an identity gets its own <see cref="AssumedRoleKmsClient"/>, whose
/// credentials come from STS AssumeRole with the binding as the external ID. The cache keeps a client for an identity
/// once a call through it succeeds.
/// </para>
/// <para>
/// Entries for a region are created with <see cref="LazyThreadSafetyMode.PublicationOnly"/>, so a failure to create a
/// client is not cached. Callers that race to create the same entry can each build a client, and only the client that is
/// stored is kept: the others are never stored, so <see cref="Dispose"/> cannot dispose them.
/// </para>
/// </remarks>
// See ADR-1235 and ADR-1429.
[ExcludeFromCodeCoverage(
    Justification = "Creates AWS SDK clients; a passthrough to AWSSDK.KeyManagementService."
)]
internal sealed class DefaultAwsKmsClientFactory(TimeProvider timeProvider)
    : IAwsKmsClientFactory,
        IDisposable
{
    private static readonly TimeSpan AssumedRoleIdleTimeout = TimeSpan.FromDays(1);

    private ConcurrentDictionary<
        string,
        Lazy<AmazonKeyManagementServiceClient>
    > RegionClients { get; } = new(StringComparer.Ordinal);

    private KeyAccessClientCache<AssumedRoleKmsClient> AssumedRoleClients { get; } =
        new(timeProvider, AssumedRoleIdleTimeout);

    /// <inheritdoc />
    public ValueTask<TResult> ExecuteAsync<TResult>(
        string region,
        KeyAccess? access,
        Func<IAmazonKeyManagementService, CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (access is null)
        {
            return operation(GetRegionClient(region), cancellationToken);
        }

        return AssumedRoleClients.UseAsync(
            $"{region}|{access.Identity}|{access.Binding}",
            _ => ValueTask.FromResult(AssumedRoleKmsClient.Create(region, access)),
            (assumedRole, token) => operation(assumedRole.Client, token),
            cancellationToken
        );
    }

    private AmazonKeyManagementServiceClient GetRegionClient(string region) =>
        RegionClients
            .GetOrAdd(
                region,
                static name => new Lazy<AmazonKeyManagementServiceClient>(
                    () =>
                        new AmazonKeyManagementServiceClient(
                            AwsKmsClientConfiguration.Create(name)
                        ),
                    LazyThreadSafetyMode.PublicationOnly
                )
            )
            .Value;

    /// <summary>
    /// Disposes every client that has been created, with the credentials of each assumed role.
    /// </summary>
    public void Dispose()
    {
        foreach (var lazy in RegionClients.Values)
        {
            if (lazy.IsValueCreated)
            {
                lazy.Value.Dispose();
            }
        }

        AssumedRoleClients.Dispose();
    }
}
