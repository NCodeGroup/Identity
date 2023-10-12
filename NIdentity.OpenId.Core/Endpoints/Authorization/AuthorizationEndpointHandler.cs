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

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Results;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Endpoints.Authorization;

internal class AuthorizationEndpointHandler : ICommandResponseHandler<AuthorizationEndpointCommand, IOpenIdResult>
{
    private IMediator Mediator { get; }
    private IClientStore ClientStore { get; }
    private IOpenIdErrorFactory ErrorFactory { get; }

    public AuthorizationEndpointHandler(IMediator mediator, IClientStore clientStore, IOpenIdErrorFactory errorFactory)
    {
        Mediator = mediator;
        ClientStore = clientStore;
        ErrorFactory = errorFactory;
    }

    /// <inheritdoc />
    public async ValueTask<IOpenIdResult> HandleAsync(AuthorizationEndpointCommand command, CancellationToken cancellationToken)
    {
        var endpointContext = command.EndpointContext;

        var authorizationSource = await LoadAuthorizationSourceAsync(
            endpointContext,
            cancellationToken);

        IBaseAuthorizationRequest authorizationMessage = authorizationSource;

        try
        {
            var authorizationContext = await LoadAuthorizationRequestAsync(
                authorizationSource,
                cancellationToken);

            var authorizationRequest = authorizationContext.AuthorizationRequest;
            authorizationMessage = authorizationRequest;

            await ValidateAuthorizationRequestAsync(
                authorizationContext,
                cancellationToken);

            var authenticateResult = await AuthenticateAsync(
                endpointContext,
                cancellationToken);

            if (!authenticateResult.Succeeded)
            {
                var error = ErrorFactory
                    .AccessDenied("An error occured while attempting to authenticate the end-user.")
                    .WithException(authenticateResult.Failure);

                return await DetermineErrorResultAsync(
                    authorizationMessage,
                    error,
                    cancellationToken);
            }

            var authenticationTicket = authenticateResult.Ticket;

            var authorizeResult = await AuthorizeAsync(
                endpointContext,
                authorizationContext,
                authenticationTicket,
                cancellationToken);

            if (authorizeResult != null)
                return authorizeResult;

            var authorizationTicket = await CreateAuthorizationTicketAsync(
                endpointContext,
                authorizationContext,
                authenticationTicket,
                cancellationToken);

            return new AuthorizationResult(
                authorizationRequest.RedirectUri,
                authorizationRequest.ResponseMode,
                authorizationTicket);
        }
        catch (Exception exception)
        {
            return await DetermineErrorResultAsync(
                authorizationMessage,
                exception,
                cancellationToken);
        }
    }

    private async ValueTask<IAuthorizationSource> LoadAuthorizationSourceAsync(
        OpenIdEndpointContext endpointContext,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new LoadAuthorizationSourceCommand(endpointContext),
            cancellationToken);

    private async ValueTask<AuthorizationContext> LoadAuthorizationRequestAsync(
        IAuthorizationSource authorizationSource,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new LoadAuthorizationRequestCommand(authorizationSource),
            cancellationToken);

    private async ValueTask ValidateAuthorizationRequestAsync(
        AuthorizationContext authorizationContext,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new ValidateAuthorizationRequestCommand(authorizationContext),
            cancellationToken);

    private async ValueTask<AuthenticateResult> AuthenticateAsync(
        OpenIdEndpointContext endpointContext,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new AuthenticateCommand(endpointContext),
            cancellationToken);

    private async ValueTask<IOpenIdResult?> AuthorizeAsync(
        OpenIdEndpointContext endpointContext,
        AuthorizationContext authorizationContext,
        AuthenticationTicket authenticationTicket,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new AuthorizeCommand(
                endpointContext,
                authorizationContext,
                authenticationTicket),
            cancellationToken);

    private async Task<IAuthorizationTicket> CreateAuthorizationTicketAsync(
        OpenIdEndpointContext endpointContext,
        AuthorizationContext authorizationContext,
        AuthenticationTicket authenticationTicket,
        CancellationToken cancellationToken) =>
        await Mediator.SendAsync(
            new CreateAuthorizationTicketCommand(
                endpointContext,
                authorizationContext,
                authenticationTicket),
            cancellationToken);

    private async ValueTask<IOpenIdResult> DetermineErrorResultAsync(
        IBaseAuthorizationRequest authorizationMessage,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var error = exception is OpenIdException openIdException ?
            openIdException.Error :
            ErrorFactory
                .Create(OpenIdConstants.ErrorCodes.ServerError)
                .WithException(exception);

        return await DetermineErrorResultAsync(
            authorizationMessage,
            error,
            cancellationToken);
    }

    private async ValueTask<IOpenIdResult> DetermineErrorResultAsync(
        IBaseAuthorizationRequest authorizationMessage,
        IOpenIdError error,
        CancellationToken cancellationToken)
    {
        // before we redirect, we must validate the client_id and redirect_uri
        // otherwise we must return a failure HTTP status code

        if (authorizationMessage is IAuthorizationRequest authorizationRequest)
        {
            var state = authorizationRequest.State;
            var redirectUri = authorizationRequest.RedirectUri;
            var responseMode = authorizationRequest.ResponseMode;

            if (!string.IsNullOrEmpty(state))
            {
                error.State = state;
            }

            if (string.IsNullOrEmpty(authorizationRequest.ClientId))
            {
                return new OpenIdErrorResult(error);
            }

            var client = await ClientStore.TryGetByClientIdAsync(authorizationRequest.ClientId, cancellationToken);
            if (client == null)
            {
                return new OpenIdErrorResult(error);
            }

            if (!client.RedirectUris.Contains(redirectUri) && !(client.AllowLoopback && redirectUri.IsLoopback))
            {
                return new OpenIdErrorResult(error);
            }

            return new AuthorizationResult(redirectUri, responseMode, error);
        }
        else
        {
            if (authorizationMessage.TryGetValue(OpenIdConstants.Parameters.State, out var state) && !StringValues.IsNullOrEmpty(state))
            {
                error.State = state;
            }

            if (!authorizationMessage.TryGetValue(OpenIdConstants.Parameters.ResponseMode, out var responseModeStringValues) || !Enum.TryParse(responseModeStringValues, out ResponseMode responseMode))
            {
                responseMode = ResponseMode.Query;
            }

            if (!authorizationMessage.TryGetValue(OpenIdConstants.Parameters.RedirectUri, out var redirectUrl) || !Uri.TryCreate(redirectUrl, UriKind.Absolute, out var redirectUri))
            {
                return new OpenIdErrorResult(error);
            }

            if (!authorizationMessage.TryGetValue(OpenIdConstants.Parameters.ClientId, out var clientId) || StringValues.IsNullOrEmpty(clientId))
            {
                return new OpenIdErrorResult(error);
            }

            var client = await ClientStore.TryGetByClientIdAsync(clientId.ToString(), cancellationToken);
            if (client == null)
            {
                return new OpenIdErrorResult(error);
            }

            if (!client.RedirectUris.Contains(redirectUri) && !(client.AllowLoopback && redirectUri.IsLoopback))
            {
                return new OpenIdErrorResult(error);
            }

            return new AuthorizationResult(redirectUri, responseMode, error);
        }
    }
}
