﻿#region Copyright Preamble

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

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Endpoints.Authorization;

internal class AuthorizationEndpointHandler : ICommandResponseHandler<AuthorizationEndpointCommand, IOpenIdResult>
{
    private IMediator Mediator { get; }
    private IClientStore ClientStore { get; }
    private IOpenIdErrorFactory OpenIdErrorFactory { get; }

    public AuthorizationEndpointHandler(IMediator mediator, IClientStore clientStore, IOpenIdErrorFactory openIdErrorFactory)
    {
        Mediator = mediator;
        ClientStore = clientStore;
        OpenIdErrorFactory = openIdErrorFactory;
    }

    /// <inheritdoc />
    public async ValueTask<IOpenIdResult> HandleAsync(AuthorizationEndpointCommand command, CancellationToken cancellationToken)
    {
        var endpointContext = command.EndpointContext;

        var authorizationRequestStringValues = await GetAuthorizationRequestStringValues(endpointContext, cancellationToken);
        IOpenIdMessage openIdMessage = authorizationRequestStringValues;

        try
        {
            var authorizationRequestUnion = await GetAuthorizationRequestUnionAsync(authorizationRequestStringValues, cancellationToken);
            openIdMessage = authorizationRequestUnion;

            await ValidateAuthorizationRequestAsync(authorizationRequestUnion, cancellationToken);

            var authenticateResult = await AuthenticateAsync(endpointContext, cancellationToken);

            var result = await AuthorizeAsync(
                endpointContext,
                authorizationRequestUnion,
                authenticateResult,
                cancellationToken);

            if (result != null)
                return result;

            var authorizationTicket = await CreateAuthorizationTicketAsync(
                endpointContext,
                authorizationRequestUnion,
                authenticateResult,
                cancellationToken);

            return new AuthorizationResult(
                authorizationRequestUnion.RedirectUri,
                authorizationRequestUnion.ResponseMode,
                authorizationTicket);
        }
        catch (Exception exception)
        {
            var result = await DetermineErrorResultAsync(endpointContext, openIdMessage, exception);
            if (result != null)
            {
                return result;
            }

            throw;
        }
    }

    private async ValueTask<IAuthorizationRequestStringValues> GetAuthorizationRequestStringValues(
        OpenIdEndpointContext endpointContext,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new GetAuthorizationCommandStringValuesCommand(endpointContext),
            cancellationToken);

    private async ValueTask<IAuthorizationRequestUnion> GetAuthorizationRequestUnionAsync(
        IAuthorizationRequestStringValues message,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new GetAuthorizationCommandUnionCommand(message),
            cancellationToken);

    private async ValueTask ValidateAuthorizationRequestAsync(
        IAuthorizationRequestUnion authorizationRequest,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new ValidateAuthorizationRequestCommand(authorizationRequest),
            cancellationToken);

    private async ValueTask<AuthenticateResult> AuthenticateAsync(
        OpenIdEndpointContext endpointContext,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new AuthenticateCommand(endpointContext),
            cancellationToken);

    private async ValueTask<IOpenIdResult?> AuthorizeAsync(
        OpenIdEndpointContext endpointContext,
        IAuthorizationRequestUnion authorizationRequest,
        AuthenticateResult authenticateResult,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new AuthorizeCommand(
                endpointContext,
                authorizationRequest,
                authenticateResult),
            cancellationToken);

    private async Task<IAuthorizationTicket> CreateAuthorizationTicketAsync(
        OpenIdEndpointContext endpointContext,
        IAuthorizationRequestUnion authorizationRequest,
        AuthenticateResult authenticateResult,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new CreateAuthorizationTicketCommand(
                endpointContext,
                authorizationRequest,
                authenticateResult),
            cancellationToken);

    private async ValueTask<IOpenIdResult?> DetermineErrorResultAsync(OpenIdEndpointContext context, IOpenIdMessage message, Exception exception)
    {
        // before we redirect, we must validate the client_id and redirect_uri
        // otherwise we must return a failure HTTP status code

        var httpContext = context.HttpContext;
        var cancellationToken = httpContext.RequestAborted;

        var error = exception is OpenIdException openIdException ?
            openIdException.Error :
            OpenIdErrorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithException(exception);

        if (message is IAuthorizationRequestUnion authorizationRequest)
        {
            var state = authorizationRequest.State;
            var client = authorizationRequest.Client;
            var redirectUri = authorizationRequest.RedirectUri;
            var responseMode = authorizationRequest.ResponseMode;

            if (!string.IsNullOrEmpty(state))
            {
                error.State = state;
            }

            if (!client.RedirectUris.Contains(redirectUri) && !(client.AllowLoopback && redirectUri.IsLoopback))
            {
                return null;
            }

            return new AuthorizationResult(redirectUri, responseMode, error);
        }
        else
        {
            if (message.TryGetValue(OpenIdConstants.Parameters.State, out var state) && !StringValues.IsNullOrEmpty(state))
            {
                error.State = state;
            }

            if (!message.TryGetValue(OpenIdConstants.Parameters.ResponseMode, out var responseModeStringValues) || !Enum.TryParse(responseModeStringValues, out ResponseMode responseMode))
            {
                responseMode = ResponseMode.Query;
            }

            if (!message.TryGetValue(OpenIdConstants.Parameters.RedirectUri, out var redirectUrl) || !Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri))
            {
                return null;
            }

            if (!message.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientId) || string.IsNullOrEmpty(clientId))
            {
                return null;
            }

            var client = await ClientStore.TryGetByClientIdAsync(clientId, cancellationToken);
            if (client == null)
            {
                return null;
            }

            if (!client.RedirectUris.Contains(redirectUri) && !(client.AllowLoopback && redirectUri.IsLoopback))
            {
                return null;
            }

            return new AuthorizationResult(redirectUri, responseMode, error);
        }
    }
}
