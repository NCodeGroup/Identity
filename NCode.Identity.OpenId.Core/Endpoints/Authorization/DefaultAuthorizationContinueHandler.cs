#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Clients;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Endpoints.Authorization.Models;
using NCode.Identity.OpenId.Endpoints.Continue;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueProvider"/> abstraction for handling authorization continuations.
/// </summary>
public class DefaultAuthorizationContinueProvider(
    IClientAuthenticationService clientAuthenticationService,
    IAuthorizationEndpointLogic authorizationEndpointLogic
) : IContinueProvider
{
    private IClientAuthenticationService ClientAuthenticationService { get; } = clientAuthenticationService;
    private IAuthorizationEndpointLogic AuthorizationEndpointLogic { get; } = authorizationEndpointLogic;

    /// <inheritdoc />
    public string ContinueCode => OpenIdConstants.ContinueCodes.Authorization;

    /// <inheritdoc />
    public async ValueTask<EndpointDisposition> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;

        var authResult = await ClientAuthenticationService.AuthenticateClientAsync(
            openIdContext,
            cancellationToken
        );

        if (!authResult.HasClient)
        {
            var error = authResult.Error ?? errorFactory.InvalidClient();
            error.StatusCode = StatusCodes.Status400BadRequest;
            return EndpointDisposition.Handled(error.AsHttpResult());
        }

        var openIdClient = authResult.Client;
        var authorizationRequest = continuePayloadJson.Deserialize<IAuthorizationRequest>(openIdEnvironment.JsonSerializerOptions);
        if (authorizationRequest == null)
            throw new InvalidOperationException("JSON deserialization returned null.");

        authorizationRequest.IsContinuation = true;

        var clientRedirectContext = new ClientRedirectContext(
            authorizationRequest.RedirectUri,
            authorizationRequest.ResponseMode,
            authorizationRequest.State
        );

        var disposition = await AuthorizationEndpointLogic.ProcessRequestAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            clientRedirectContext,
            cancellationToken
        );
        return disposition;
    }
}
