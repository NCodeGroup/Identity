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

using System.Diagnostics;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Token.ClientCredentials;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenGrantCommand{TGrant}"/> message
/// with <see cref="ClientCredentialsGrant"/>.
/// </summary>
public class DefaultValidateClientCredentialsGrantHandler(
    IOpenIdErrorFactory errorFactory
) : ICommandHandler<ValidateTokenGrantCommand<ClientCredentialsGrant>>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenGrantCommand<ClientCredentialsGrant> command,
        CancellationToken cancellationToken)
    {
        var (_, openIdClient, tokenRequest, clientCredentialsGrant) = command;

        // DefaultClientAuthenticationService already performs this check for us
        Debug.Assert(
            string.IsNullOrEmpty(tokenRequest.ClientId) ||
            string.Equals(openIdClient.ClientId, tokenRequest.ClientId, StringComparison.Ordinal));

        // see DefaultValidateTokenRequestHandler for additional validation such as scope, etc

        // TODO...
        throw new NotImplementedException();
    }
}
