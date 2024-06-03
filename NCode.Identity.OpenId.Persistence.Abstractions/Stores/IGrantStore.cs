#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for persisting any type of grant and their payloads to storage.
/// </summary>
public interface IGrantStore : IStore<PersistedGrant>
{
    ValueTask<PersistedGrant?> TryGetAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        CancellationToken cancellationToken);

    ValueTask SetConsumedOnceAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset consumedWhen,
        CancellationToken cancellationToken);

    ValueTask SetRevokedOnceAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset revokedWhen,
        CancellationToken cancellationToken);

    ValueTask UpdateExpirationAsync(
        string tenantId,
        string grantType,
        string hashedKey,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken);
}
