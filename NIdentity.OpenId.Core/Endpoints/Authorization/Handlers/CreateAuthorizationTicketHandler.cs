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

using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Logic.Authorization;
using NIdentity.OpenId.Mediator;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class CreateAuthorizationTicketHandler :
    ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private ISystemClock SystemClock { get; }
    private IAuthorizationTicketService AuthorizationTicketService { get; }

    public async ValueTask<IAuthorizationTicket> HandleAsync(
        CreateAuthorizationTicketCommand command,
        CancellationToken cancellationToken)
    {
        var endpointContext = command.EndpointContext;
        var authorizationContext = command.AuthorizationContext;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var messageContext = authorizationRequest.OpenIdMessageContext;
        var responseType = authorizationRequest.ResponseType;

        var ticket = AuthorizationTicket.Create(messageContext);

        ticket.CreatedWhen = SystemClock.UtcNow;
        ticket.State = authorizationRequest.State;
        ticket.Issuer = endpointContext.Tenant.Issuer;

        if (responseType.HasFlag(ResponseTypes.Code))
        {
            await AuthorizationTicketService.CreateAuthorizationCodeAsync(
                command,
                ticket,
                cancellationToken);
        }

        if (responseType.HasFlag(ResponseTypes.Token))
        {
            await AuthorizationTicketService.CreateAccessTokenAsync(
                command,
                ticket,
                cancellationToken);
        }

        if (responseType.HasFlag(ResponseTypes.IdToken))
        {
            await AuthorizationTicketService.CreateIdTokenAsync(
                command,
                ticket,
                cancellationToken);
        }

        return ticket;
    }
}
