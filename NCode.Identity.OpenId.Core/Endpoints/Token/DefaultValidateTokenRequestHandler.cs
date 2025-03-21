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

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Endpoints.Token;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenRequestCommand"/> messsage.
/// </summary>
public class DefaultValidateTokenRequestHandler : ICommandHandler<ValidateTokenRequestCommand>, ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultOpenIdRegistration.MediatorPriorityHigh;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (openIdContext, openIdClient, tokenRequest) = command;

        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        // DefaultClientAuthenticationService already performs this check for us
        Debug.Assert(
            string.IsNullOrEmpty(tokenRequest.ClientId) ||
            string.Equals(openIdClient.ClientId, tokenRequest.ClientId, StringComparison.Ordinal));

        // scopes_supported
        var requestedScopes = tokenRequest.Scopes;
        var hasInvalidScopes = requestedScopes?.Except(settings.GetValue(SettingKeys.ScopesSupported)).Any() ?? false;
        if (hasInvalidScopes)
            // invalid_scope
            throw errorFactory
                .InvalidScope()
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();

        // additional validation occurs in:
        // - DefaultSelectTokenGrantHandlerHandler
        // - The specific token grant handler

        return ValueTask.CompletedTask;
    }
}
