﻿#region Copyright Preamble

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

internal class EmptyClientStore : IClientStore
{
    /// <inheritdoc />
    public ValueTask AddAsync(Client client, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask RemoveByIdAsync(long id, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<Client?> TryGetByIdAsync(long id, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<Client?>(null);
    }

    /// <inheritdoc />
    public ValueTask<Client?> TryGetByClientIdAsync(string tenantId, string clientId, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<Client?>(null);
    }
}
