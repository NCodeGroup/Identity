#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Playground.DataLayer;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Playground.Stores;

internal class ClientStore : IClientStore
{
    private IdentityDbContext Context { get; }
    private IMapper Mapper { get; }

    public ClientStore(IdentityDbContext context, IMapper mapper)
    {
        Context = context;
        Mapper = mapper;
    }

    /// <inheritdoc />
    public async ValueTask<Client?> TryGetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var clientEntity = await Context.Clients.FirstOrDefaultAsync(_ => _.Id == id, cancellationToken);
        var client = Mapper.Map<Client>(clientEntity);
        return client;
    }

    /// <inheritdoc />
    public async ValueTask<Client?> TryGetByClientIdAsync(string clientId, CancellationToken cancellationToken)
    {
        var normalizedClientId = clientId.ToUpperInvariant();
        var clientEntity = await Context.Clients.FirstOrDefaultAsync(_ => _.NormalizedClientId == normalizedClientId, cancellationToken);
        var client = Mapper.Map<Client>(clientEntity);
        return client;
    }
}
