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
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Commands;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Errors;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateTokenRequestCommand"/> messsage.
/// </summary>
internal class DefaultValidateTokenRequestHandler
    : ICommandHandler<ValidateTokenRequestCommand>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateTokenRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, tokenRequest) = command;

        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        // client_id/authenticated-client match is enforced upstream by DefaultClientAuthenticationService

        // scopes_supported
        var requestedScopes = tokenRequest.Scopes;
        var hasInvalidScopes =
            requestedScopes?.Except(settings.GetValue(OpenIdSettingKeys.ScopesSupported)).Any()
            ?? false;
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
