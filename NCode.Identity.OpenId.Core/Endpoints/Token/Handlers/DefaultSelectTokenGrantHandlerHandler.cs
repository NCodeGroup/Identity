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
using NCode.Identity.OpenId.Endpoints.Token.Logic;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints.Token.Handlers;

/// <summary>
/// Provides a default implementation of handler for the <see cref="SelectTokenGrantHandlerCommand"/> message.
/// </summary>
/// <remarks>
/// No, the duplicate ...Handler term is not a typo as this is handler that returns a handler.
/// </remarks>
public class DefaultSelectTokenGrantHandlerHandler(
    IOpenIdErrorFactory errorFactory,
    IEnumerable<ITokenGrantHandler> handlers
) : ICommandResponseHandler<SelectTokenGrantHandlerCommand, ITokenGrantHandler>
{
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    private Dictionary<string, ITokenGrantHandler> HandlersByGrantType { get; } = handlers
        .SelectMany(handler => handler.GrantTypes.Select(grantType => (grantType, handler)))
        .ToDictionary(pair => pair.grantType, pair => pair.handler, StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<ITokenGrantHandler> HandleAsync(
        SelectTokenGrantHandlerCommand command,
        CancellationToken cancellationToken)
    {
        var (_, openIdClient, tokenRequest) = command;

        var clientSettings = openIdClient.Settings;
        var grantType = tokenRequest.GrantType;

        cancellationToken.ThrowIfCancellationRequested();

        // grant_type
        if (string.IsNullOrEmpty(grantType))
        {
            // invalid_request
            throw ErrorFactory
                .MissingParameter(OpenIdConstants.Parameters.GrantType)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // grant_types_supported
        if (!clientSettings.GrantTypesSupported.Contains(grantType))
        {
            // unauthorized_client
            throw ErrorFactory
                .UnauthorizedClient("The provided grant type is not allowed by the client.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        if (!HandlersByGrantType.TryGetValue(grantType, out var handler))
        {
            // unsupported_grant_type
            throw ErrorFactory
                .UnsupportedGrantType("The provided grant type is not supported by the authorization server.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(handler);
    }
}
