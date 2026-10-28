#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Commands;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.DeviceAuthorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateDeviceAuthorizationRequestCommand"/>
/// message.
/// </summary>
internal class DefaultValidateDeviceAuthorizationRequestHandler
    : ICommandHandler<ValidateDeviceAuthorizationRequestCommand>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateDeviceAuthorizationRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, _) = command;
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        // The device authorization grant must be allowed by the client (RFC 8628).
        if (
            settings.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out var grantTypesSupported)
            && !grantTypesSupported.Contains(OpenIdProtocol.GrantTypes.DeviceCode)
        )
        {
            throw errorFactory
                .UnauthorizedClient("The device authorization grant is not allowed by the client.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        return ValueTask.CompletedTask;
    }
}
