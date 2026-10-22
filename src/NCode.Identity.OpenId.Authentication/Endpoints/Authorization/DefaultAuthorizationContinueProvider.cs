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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Results;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the <see cref="IContinueProvider"/> abstraction for handling authorization continuations.
/// </summary>
internal class DefaultAuthorizationContinueProvider(
    IOpenIdClientResolver clientResolver,
    IAuthorizationRequestProcessor authorizationRequestProcessor,
    IAuthorizationResponseDeliveryResolver deliveryResolver
) : IContinueProvider
{
    private IOpenIdClientResolver ClientResolver { get; } = clientResolver;
    private IAuthorizationRequestProcessor AuthorizationRequestProcessor { get; } =
        authorizationRequestProcessor;
    private IAuthorizationResponseDeliveryResolver DeliveryResolver { get; } = deliveryResolver;

    /// <inheritdoc />
    public string ContinueCode => OpenIdConstants.ContinueCodes.Authorization;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyEndpointDisposition> ContinueAsync(
        OpenIdContext openIdContext,
        JsonElement continuePayloadJson,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;

        var authorizationRequest = continuePayloadJson.Deserialize<IAuthorizationRequest>(
            openIdEnvironment.JsonSerializerOptions
        );
        if (authorizationRequest == null)
            throw new InvalidOperationException("JSON deserialization returned null.");

        // The consumed-once continuation grant is itself the authorization proof (the original request already
        // identified and validated the client), and the credential-less resumption request carries nothing to
        // authenticate. Resolve the client the continuation captured rather than re-authenticating it.
        var openIdClient = await ClientResolver.ResolveClientOrDefaultAsync(
            openIdContext,
            authorizationRequest.ClientId,
            cancellationToken
        );
        if (openIdClient is null)
        {
            var error = errorFactory.InvalidClient();
            error.StatusCode = StatusCodes.Status400BadRequest;
            return ReadOnlyEndpointDisposition.Handled(error.AsHttpResult());
        }

        authorizationRequest.IsContinuation = true;

        // The parked request's redirect_uri was checked against the client's registration when it arrived.
        var delivery = await DeliveryResolver.ResolveAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            authorizationRequest.RedirectUri,
            cancellationToken
        );

        var disposition = await AuthorizationRequestProcessor.ProcessRequestAsync(
            openIdContext,
            openIdClient,
            authorizationRequest,
            delivery,
            cancellationToken
        );
        return disposition;
    }
}
