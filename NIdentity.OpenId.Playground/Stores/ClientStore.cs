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
    private readonly IdentityDbContext _context;
    private readonly IMapper _mapper;

    public ClientStore(IdentityDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async ValueTask<Client?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var clientEntity = await _context.Clients.FirstOrDefaultAsync(_ => _.Id == id, cancellationToken);
        var client = _mapper.Map<Client>(clientEntity);
        return client;
    }

    /// <inheritdoc />
    public async ValueTask<Client?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken)
    {
        var normalizedClientId = clientId.ToUpperInvariant();
        var clientEntity = await _context.Clients.FirstOrDefaultAsync(_ => _.NormalizedClientId == normalizedClientId, cancellationToken);
        var client = _mapper.Map<Client>(clientEntity);
        return client;
    }
}