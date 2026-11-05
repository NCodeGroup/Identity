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
/// and the credentials that call AssumeRole come from the default chain.
/// </para>
/// </remarks>
// See ADR-1235.
[ExcludeFromCodeCoverage(
    Justification = "Creates AWS SDK clients; a passthrough to AWSSDK.KeyManagementService."
)]
internal sealed class DefaultAwsKmsClientFactory : IAwsKmsClientFactory, IDisposable
{
    private const string RoleSessionName = "ncode-identity-root-key";

    private ConcurrentDictionary<string, Lazy<AmazonKeyManagementServiceClient>> Clients { get; } =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public IAmazonKeyManagementService GetClient(string region, KeyAccess? access)
    {
        if (access is null)
        {
            return Clients
                .GetOrAdd(
                    region,
                    static name => new Lazy<AmazonKeyManagementServiceClient>(() =>
                        new AmazonKeyManagementServiceClient(RegionEndpoint.GetBySystemName(name))
                    )
                )
                .Value;
        }

        return Clients
            .GetOrAdd(
                $"{region}|{access.Identity}|{access.Binding}",
                static (_, state) =>
                    new Lazy<AmazonKeyManagementServiceClient>(() =>
                        CreateAssumedRoleClient(state.Region, state.Access)
                    ),
                (Region: region, Access: access)
            )
            .Value;
    }

    private static AmazonKeyManagementServiceClient CreateAssumedRoleClient(
        string region,
        KeyAccess access
    )
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

        return new AmazonKeyManagementServiceClient(credentials, config);
    }

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
