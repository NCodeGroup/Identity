// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="ValidateBackchannelAuthenticationRequestCommand"/>
/// message.
/// </summary>
internal class DefaultValidateBackchannelAuthenticationRequestHandler
    : ICommandHandler<ValidateBackchannelAuthenticationRequestCommand>,
        ISupportMediatorPriority
{
    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        ValidateBackchannelAuthenticationRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, request) = command;
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        // The CIBA grant must be allowed by the client (ADR-1085).
        if (
            settings.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out var grantTypesSupported)
            && !grantTypesSupported.Contains(OpenIdProtocol.GrantTypes.Ciba)
        )
        {
            throw errorFactory
                .UnauthorizedClient("The CIBA grant is not allowed by the client.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // Exactly one of login_hint, login_hint_token, or id_token_hint is required (CIBA §7.1).
        var hintCount =
            (string.IsNullOrEmpty(request.LoginHint) ? 0 : 1)
            + (string.IsNullOrEmpty(request.LoginHintToken) ? 0 : 1)
            + (string.IsNullOrEmpty(request.IdTokenHint) ? 0 : 1);
        if (hintCount != 1)
        {
            throw errorFactory
                .InvalidRequest(
                    "Exactly one of login_hint, login_hint_token, or id_token_hint is required."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // The client's configured delivery mode must be one the server supports.
        var deliveryMode =
            settings.GetValue(OpenIdSettingKeys.BackchannelTokenDeliveryMode)
            ?? OpenIdProtocol.BackchannelTokenDeliveryModes.Poll;

        var deliveryModesSupported = settings.GetValue(
            OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported
        );
        if (!deliveryModesSupported.Contains(deliveryMode))
        {
            throw errorFactory
                .InvalidRequest($"The '{deliveryMode}' token delivery mode is not supported.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // Ping and push delivery require a client_notification_token (CIBA §7.1).
        var isPoll = string.Equals(
            deliveryMode,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
            StringComparison.Ordinal
        );
        if (!isPoll && string.IsNullOrEmpty(request.ClientNotificationToken))
        {
            throw errorFactory
                .InvalidRequest(
                    "A client_notification_token is required for ping and push delivery."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        // Ping and push delivery POST to the client's registered client_notification_endpoint (CIBA §7.3/§10).
        if (
            !isPoll
            && string.IsNullOrEmpty(settings.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint))
        )
        {
            throw errorFactory
                .InvalidRequest(
                    "A client_notification_endpoint must be registered for ping and push delivery."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsException();
        }

        return ValueTask.CompletedTask;
    }
}
