#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.Jose;
using NCode.Identity.OpenId.Logic;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Tokens.Commands;

namespace NCode.Identity.OpenId.Tokens.Handlers;

/// <summary>
/// Provides a default implementation for a <see cref="GetAccessTokenPayloadClaimsCommand"/> handler that generates the
/// payload claims for an id token.
/// </summary>
/// <remarks>
/// This handler is responsible for generating protocol claims and any claims derived from the subject are handled by
/// <see cref="DefaultGetAccessTokenSubjectClaimsHandler"/>.
/// </remarks>
public class DefaultGetAccessTokenPayloadClaimsHandler(
    ICryptoService cryptoService
) : ICommandHandler<GetAccessTokenPayloadClaimsCommand>
{
    private ICryptoService CryptoService { get; } = cryptoService;

    /// <inheritdoc />
    public ValueTask HandleAsync(GetAccessTokenPayloadClaimsCommand command, CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, _, payloadClaims) = command;

        var tenant = openIdContext.Tenant;

        if (!payloadClaims.ContainsKey(JoseClaimNames.Payload.Jti))
        {
            const int byteLength = 16; // aka 128 bits which is larger than the entropy of GUID v4 (122 bits)
            var tokenId = CryptoService.GenerateUrlSafeKey(byteLength);

            payloadClaims[JoseClaimNames.Payload.Jti] = tokenId;
        }

        payloadClaims[JoseClaimNames.Payload.Tid] = tenant.TenantId;
        payloadClaims[JoseClaimNames.Payload.ClientId] = openIdClient.ClientId;

        // TODO
        // sid
        // cnf
        // scope (except offline_access unless subject is present)
        // [optional subject claims (acr)]
        // [api resource claims]
        // [api scope claims]

        return ValueTask.CompletedTask;
    }
}
