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
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class CreateAuthorizationTicketHandler : ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private IOpenIdContext OpenIdContext { get; }

    public CreateAuthorizationTicketHandler(IOpenIdContext openIdContext)
    {
        OpenIdContext = openIdContext;
    }

    public async ValueTask<IAuthorizationTicket> HandleAsync(CreateAuthorizationTicketCommand command, CancellationToken cancellationToken)
    {
        var endpointContext = command.EndpointContext;
        var authorizationRequest = command.AuthorizationRequest;
        var authenticateResult = command.AuthenticateResult;
        var responseType = authorizationRequest.ResponseType;

        var ticket = new AuthorizationTicket(OpenIdContext)
        {
            State = authorizationRequest.State
        };

        if (responseType.HasFlag(ResponseTypes.Code))
        {
            // TODO
            ticket.Code = "TODO";
            throw new NotImplementedException();
        }

        if (responseType.HasFlag(ResponseTypes.IdToken))
        {
            // TODO
            ticket.IdToken = "TODO";
            throw new NotImplementedException();
        }

        if (responseType.HasFlag(ResponseTypes.Token))
        {
            // TODO
            ticket.TokenType = "Bearer"; // TODO: use constant
            ticket.ExpiresIn = TimeSpan.Zero;
            ticket.AccessToken = "TODO";
            throw new NotImplementedException();
        }

        ticket.Issuer = "TODO";

        return ticket;
    }
}
