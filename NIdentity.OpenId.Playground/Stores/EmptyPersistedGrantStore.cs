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

using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Playground.Stores;

internal class EmptyPersistedGrantStore : IPersistedGrantStore
{
    /// <inheritdoc />
    public ValueTask AddAsync(PersistedGrant item, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveByIdAsync(long id, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<PersistedGrant?> TryGetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<PersistedGrant?>(null);
    }

    /// <inheritdoc />
    public ValueTask<PersistedGrant?> TryGetAsync(string tenantId, string grantType, string hashedKey, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<PersistedGrant?>(null);
    }

    /// <inheritdoc />
    public ValueTask SetConsumedOnceAsync(string tenantId, string grantType, string hashedKey, DateTimeOffset consumedWhen, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
