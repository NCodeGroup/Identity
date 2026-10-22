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

using NCode.Identity.SharedState;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// Handles the announcement that a tenant changed by evicting the tenant from this replica's
/// <see cref="IOpenIdTenantCache"/>. Every replica receives the announcement, the one that made the change included, so
/// the next request on any replica materializes the tenant again. The message is the tenant identifier. Evicting is
/// idempotent, so the second eviction on the replica that made the change, and a repeated message, are harmless.
/// </summary>
// See ADR-0205.
internal sealed class TenantChangeBroadcastHandler(IOpenIdTenantCache tenantCache)
    : IReplicaBroadcastHandler
{
    /// <summary>
    /// The name of the replica broadcast channel on which a changed tenant is announced.
    /// </summary>
    public const string ChannelName = "tenant-changed";

    private IOpenIdTenantCache TenantCache { get; } = tenantCache;

    /// <inheritdoc />
    public string Channel => ChannelName;

    /// <inheritdoc />
    public ValueTask HandleAsync(string message, CancellationToken cancellationToken) =>
        TenantCache.RemoveAsync(message, PropertyBagFactory.Create(), cancellationToken);
}
