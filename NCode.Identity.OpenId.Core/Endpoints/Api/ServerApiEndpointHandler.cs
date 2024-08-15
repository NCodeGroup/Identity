#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using Microsoft.AspNetCore.Routing;

namespace NCode.Identity.OpenId.Endpoints.Api;

/*

GET api/server/settings
PUT api/server/settings

GET api/server/secrets
POST api/server/secrets
GET api/server/secrets/{secretId}
PUT api/server/secrets/{secretId}

GET api/tenants
GET api/tenants/{tenantId}
GET api/tenants/{tenantId}/settings
GET api/tenants/{tenantId}/secrets

*/

public class ServerApiEndpointHandler : IOpenIdEndpointProvider
{
    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        throw new NotImplementedException();
    }
}
