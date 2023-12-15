#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

using System.Text.Json;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints;

namespace NIdentity.OpenId.Clients;

public interface IOpenIdClientFactory
{
    ValueTask<OpenIdClient> CreateAsync(
        OpenIdContext openIdContext,
        Client clientModel,
        CancellationToken cancellationToken);

    ValueTask<OpenIdAuthenticatedClient> CreateAsync(
        OpenIdClient publicClient,
        string method,
        SecretKey secretKey,
        JsonElement? confirmation,
        CancellationToken cancellationToken);
}
