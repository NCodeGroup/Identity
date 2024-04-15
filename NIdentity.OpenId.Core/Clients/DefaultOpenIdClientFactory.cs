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
using Microsoft.Extensions.DependencyInjection;
using NCode.Jose.SecretKeys;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints;

namespace NIdentity.OpenId.Clients;

internal class DefaultOpenIdClientFactory(
    IServiceProvider serviceProvider
) : IOpenIdClientFactory
{
    private IServiceProvider ServiceProvider { get; } = serviceProvider;

    /// <inheritdoc />
    public virtual ValueTask<OpenIdClient> CreateAsync(
        OpenIdContext openIdContext,
        Client clientModel,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<OpenIdClient>(
            ActivatorUtilities.CreateInstance<DefaultOpenIdClient>(
                ServiceProvider,
                clientModel));
    }

    /// <inheritdoc />
    public virtual ValueTask<OpenIdAuthenticatedClient> CreateAsync(
        OpenIdClient publicClient,
        string method,
        SecretKey secretKey,
        JsonElement? confirmation,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult<OpenIdAuthenticatedClient>(
            new DefaultOpenIdAuthenticatedClient(
                publicClient,
                method,
                secretKey,
                confirmation));
    }
}
