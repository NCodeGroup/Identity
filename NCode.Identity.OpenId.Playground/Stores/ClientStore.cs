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

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Playground.DataLayer.Entities;

namespace NCode.Identity.OpenId.Playground.Stores;

internal class ClientStore(
    IIdentityDbContext context,
    IMapper mapper
) : IClientStore
{
    private IIdentityDbContext DbContext { get; } = context;
    private IMapper Mapper { get; } = mapper;

    /// <inheritdoc />
    public async ValueTask AddAsync(PersistedClient persistedClient, CancellationToken cancellationToken)
    {
        var clientEntity = Mapper.Map<ClientEntity>(persistedClient);
        await DbContext.Clients.AddAsync(clientEntity, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RemoveByIdAsync(long id, CancellationToken cancellationToken)
    {
        var clientEntity = await DbContext.Clients.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (clientEntity != null)
        {
            DbContext.Clients.Remove(clientEntity);
        }
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClient?> TryGetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var clientEntity = await DbContext.Clients.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        var persistedClient = Mapper.Map<PersistedClient>(clientEntity);
        return persistedClient;
    }

    /// <inheritdoc />
    public async ValueTask<PersistedClient?> TryGetByClientIdAsync(string tenantId, string clientId, CancellationToken cancellationToken)
    {
        var clientEntity = await DbContext.Clients.FirstOrDefaultAsync(entity =>
                entity.TenantId == tenantId &&
                entity.ClientId == clientId,
            cancellationToken);
        var persistedClient = Mapper.Map<PersistedClient>(clientEntity);
        return persistedClient;
    }
}
