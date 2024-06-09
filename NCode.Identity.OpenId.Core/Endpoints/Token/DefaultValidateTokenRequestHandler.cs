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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Token;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenRequestCommand"/> messsage.
/// </summary>
public class DefaultValidateTokenRequestHandler(
    IOpenIdErrorFactory errorFactory
) : ICommandHandler<ValidateTokenRequestCommand>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (_, openIdClient, tokenRequest) = command;

        // client_id from token request
        if (!string.IsNullOrEmpty(tokenRequest.ClientId) &&
            !string.Equals(openIdClient.ClientId, tokenRequest.ClientId, StringComparison.Ordinal))
        {
            // invalid_request
            throw ErrorFactory
                .InvalidRequest("The provided client identifier does not match the authenticated client identifier.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        return ValueTask.CompletedTask;
    }
}
