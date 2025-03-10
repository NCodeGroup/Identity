﻿#region Copyright Preamble

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
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Endpoints.Token;

/// <summary>
/// Provides a default implementation of handler for the <see cref="SelectTokenGrantHandlerCommand"/> message.
/// </summary>
/// <remarks>
/// No, the duplicate ...Handler term is not a typo as this is handler that returns a handler.
/// </remarks>
public class DefaultSelectTokenGrantHandlerHandler(
    IEnumerable<ITokenGrantHandler> handlers
) : ICommandResponseHandler<SelectTokenGrantHandlerCommand, ITokenGrantHandler>
{
    private ILookup<string, ITokenGrantHandler> HandlersByGrantType { get; } = handlers
        .SelectMany(handler => handler.GrantTypes.Select(grantType => (grantType, handler)))
        .ToLookup(pair => pair.grantType, pair => pair.handler, StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<ITokenGrantHandler> HandleAsync(
        SelectTokenGrantHandlerCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenRequest) = command;

        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        cancellationToken.ThrowIfCancellationRequested();

        // grant_type
        var grantType = tokenRequest.GrantType;
        if (string.IsNullOrEmpty(grantType))
        {
            // invalid_request
            throw errorFactory
                .MissingParameter(OpenIdConstants.Parameters.GrantType)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // grant_types_supported
        if (!settings.GetValue(SettingKeys.GrantTypesSupported).Contains(grantType))
        {
            // unauthorized_client
            throw errorFactory
                .UnauthorizedClient("The provided grant type is not allowed by the client.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        ITokenGrantHandler? selectedHandler = null;
        var candidateHandlers = HandlersByGrantType[grantType];
        foreach (var candidateHandler in candidateHandlers)
        {
            if (selectedHandler != null)
            {
                // unsupported_grant_type
                throw errorFactory
                    .UnsupportedGrantType("The provided grant type has multiple handlers.")
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsException("Invalid authorization server configuration. Multiple handlers for the same grant type have been registered.");
            }

            selectedHandler = candidateHandler;
        }

        if (selectedHandler == null)
        {
            // unsupported_grant_type
            throw errorFactory
                .UnsupportedGrantType("The provided grant type is not supported by the authorization server.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(selectedHandler);
    }
}
