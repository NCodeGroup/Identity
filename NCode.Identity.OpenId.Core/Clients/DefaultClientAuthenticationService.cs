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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Clients;

internal class DefaultClientAuthenticationService(
    IOpenIdErrorFactory errorFactory,
    IEnumerable<IClientAuthenticationHandler> handlers
) : IClientAuthenticationService
{
    private IOpenIdError ErrorMultipleAuthMethods { get; } = errorFactory
        .InvalidRequest("Multiple client authentication methods were provided.")
        .WithStatusCode(StatusCodes.Status400BadRequest);

    private IEnumerable<IClientAuthenticationHandler> Handlers { get; } = handlers;

    /// <inheritdoc />
    public async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken)
    {
        var lastResult = ClientAuthenticationResult.Undefined;
        foreach (var handler in Handlers)
        {
            var result = await handler.AuthenticateClientAsync(openIdContext, cancellationToken);
            if (result.IsUndefined) continue;
            if (result.IsError) return result;
            if (!lastResult.IsUndefined) return new ClientAuthenticationResult(ErrorMultipleAuthMethods);
            lastResult = result;
        }

        return lastResult;
    }
}
