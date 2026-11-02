// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.KeyManagementService;

namespace NCode.Identity.Secrets.KeyWrapping.AwsKms.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IAwsKmsClientFactory"/>: one
/// <see cref="AmazonKeyManagementServiceClient"/> for each region, created on first use and shared by every caller.
/// </summary>
/// <remarks>
/// A client resolves its credentials through the default chain of the AWS SDK (a web identity token, a shared
/// profile, container credentials or instance credentials) on its first request, so creating a client never fails
/// for lack of credentials; the first call that uses one does.
/// </remarks>
// See ADR-1235.
[ExcludeFromCodeCoverage(
    Justification = "Creates AWS SDK clients; a passthrough to AWSSDK.KeyManagementService."
)]
internal sealed class DefaultAwsKmsClientFactory : IAwsKmsClientFactory, IDisposable
{
    private ConcurrentDictionary<string, Lazy<AmazonKeyManagementServiceClient>> Clients { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IAmazonKeyManagementService GetClient(string region) =>
        Clients
            .GetOrAdd(
                region,
                static name => new Lazy<AmazonKeyManagementServiceClient>(() =>
                    new AmazonKeyManagementServiceClient(RegionEndpoint.GetBySystemName(name))
                )
            )
            .Value;

    /// <summary>
    /// Disposes every client that has been created.
    /// </summary>
    public void Dispose()
    {
        foreach (var client in Clients.Values)
        {
            if (client.IsValueCreated)
            {
                client.Value.Dispose();
            }
        }
    }
}
