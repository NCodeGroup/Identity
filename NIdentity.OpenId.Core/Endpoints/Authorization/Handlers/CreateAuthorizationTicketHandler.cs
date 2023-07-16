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

using System.Text.Json;
using IdGen;
using NIdentity.OpenId.DataContracts;
using NIdentity.OpenId.Endpoints.Authorization.Commands;
using NIdentity.OpenId.Endpoints.Authorization.Messages;
using NIdentity.OpenId.Endpoints.Authorization.Results;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Stores;

namespace NIdentity.OpenId.Endpoints.Authorization.Handlers;

internal class CreateAuthorizationTicketHandler : ICommandResponseHandler<CreateAuthorizationTicketCommand, IAuthorizationTicket>
{
    private ISystemClock SystemClock { get; }
    private IIdGenerator<long> IdGenerator { get; }
    private IOpenIdContext OpenIdContext { get; }
    private ICryptoService CryptoService { get; }
    private IAuthorizationCodeStore AuthorizationCodeStore { get; }

    public CreateAuthorizationTicketHandler(
        ISystemClock systemClock,
        IIdGenerator<long> idGenerator,
        IOpenIdContext openIdContext,
        ICryptoService cryptoService,
        IAuthorizationCodeStore authorizationCodeStore)
    {
        SystemClock = systemClock;
        IdGenerator = idGenerator;
        OpenIdContext = openIdContext;
        CryptoService = cryptoService;
        AuthorizationCodeStore = authorizationCodeStore;
    }

    public async ValueTask<IAuthorizationTicket> HandleAsync(CreateAuthorizationTicketCommand command, CancellationToken cancellationToken)
    {
        var endpointContext = command.EndpointContext;
        var authorizationContext = command.AuthorizationContext;
        var authenticateResult = command.AuthenticateResult;
        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var responseType = authorizationRequest.ResponseType;

        var ticket = new AuthorizationTicket(OpenIdContext)
        {
            State = authorizationRequest.State
        };

        if (responseType.HasFlag(ResponseTypes.Code))
        {
            ticket.Code = await CreateAuthorizationCodeAsync(authorizationContext, cancellationToken);
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

    private async ValueTask<string> CreateAuthorizationCodeAsync(AuthorizationContext authorizationContext, CancellationToken cancellationToken)
    {
        const int byteLength = 32;
        var hexKey = CryptoService.GenerateKey(byteLength, BinaryEncodingType.Hex);
        var hashedKey = CryptoService.HashValue(hexKey, HashAlgorithmType.Sha256, BinaryEncodingType.Base64);

        var authorizationRequest = authorizationContext.AuthorizationRequest;
        var authorizationRequestJson = JsonSerializer.Serialize(authorizationRequest, OpenIdContext.JsonSerializerOptions);

        var client = authorizationContext.Client;
        var lifetime = client.AuthorizationCodeLifetime;
        var createdWhen = SystemClock.UtcNow;
        var expiresWhen = createdWhen + lifetime;

        var id = IdGenerator.CreateId();
        var authorizationCode = new AuthorizationCode
        {
            Id = id,
            HashedKey = hashedKey,
            CreatedWhen = createdWhen,
            ExpiresWhen = expiresWhen,
            AuthorizationRequestJson = authorizationRequestJson
        };

        await AuthorizationCodeStore.AddAsync(authorizationCode, cancellationToken);

        return hexKey;
    }
}
