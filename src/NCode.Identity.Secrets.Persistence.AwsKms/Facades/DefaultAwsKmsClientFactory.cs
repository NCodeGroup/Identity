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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Amazon;
using Amazon.KeyManagementService;

namespace NCode.Identity.Secrets.Persistence.AwsKms.Facades;

/// <summary>
/// Provides the default implementation of <see cref="IAwsKmsClientFactory"/>: one
/// <see cref="AmazonKeyManagementServiceClient"/> for each region, created on first use and shared by every caller.
/// </summary>
/// <remarks>
/// A client resolves its credentials through the default chain of the AWS SDK (a web identity token, a shared
/// profile, container credentials or instance credentials) on its first request, so creating a client never fails
/// for lack of credentials; the first call that uses one does.
/// </remarks>
// See ADR-0228.
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
