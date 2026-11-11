// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Results;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Identity.Time;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Handlers;

/// <summary>
/// Provides a default implementation of the <see cref="CreateBackchannelAuthenticationRequestCommand"/> handler that
/// persists the pending CIBA request keyed by a generated <c>auth_req_id</c>, notifies the authentication device, and
/// returns the <see cref="BackchannelAuthenticationResult"/>.
/// </summary>
internal class DefaultCreateBackchannelAuthenticationRequestHandler(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService,
    ICibaNotificationService notificationService
)
    : ICommandResponseHandler<
        CreateBackchannelAuthenticationRequestCommand,
        BackchannelAuthenticationResult
    >
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ICibaNotificationService NotificationService { get; } = notificationService;

    /// <inheritdoc />
    public async ValueTask<BackchannelAuthenticationResult> HandleAsync(
        CreateBackchannelAuthenticationRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, request, subjectAuthentication) = command;
        var tenantId = openIdContext.Tenant.TenantId;
        var settings = openIdClient.Settings;

        var deliveryMode =
            settings.GetValue(OpenIdSettingKeys.BackchannelTokenDeliveryMode)
            ?? OpenIdProtocol.BackchannelTokenDeliveryModes.Poll;

        var authReqId = CryptoService.GenerateUrlSafeKey();
        var lifetime = ResolveLifetime(settings, request);
        var interval = settings.GetValue(OpenIdSettingKeys.CibaPollingInterval);
        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        var payload = new CibaGrant
        {
            ClientId = openIdClient.ClientId,
            Scopes = request.Scopes ?? [],
            Resources = request.Resources,
            Status = DeviceAuthorizationStatus.Pending,
            DeliveryMode = deliveryMode,
            SubjectAuthentication = subjectAuthentication,
            LastPolledWhen = null,
            ClientNotificationToken = request.ClientNotificationToken,
            BindingMessage = request.BindingMessage,
        };

        var cibaGrant = new PersistedGrant<CibaGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = tenantId,
            ClientId = openIdClient.ClientId,
            SubjectId = subjectAuthentication.PrincipalId,
            SessionId = null,
            Payload = payload,
        };

        await PersistedGrantService.AddAsync(
            openIdContext,
            PersistedGrantService.CreateGrantId(
                tenantId,
                OpenIdConstants.PersistedGrantTypes.Ciba,
                authReqId
            ),
            cibaGrant,
            createdWhen,
            lifetime,
            cancellationToken
        );

        // Best-effort: notify the end user's authentication device that an approval is pending.
        await NotificationService.NotifyAuthenticationDeviceAsync(
            openIdContext,
            openIdClient,
            authReqId,
            payload,
            cancellationToken
        );

        return new BackchannelAuthenticationResult
        {
            AuthReqId = authReqId,
            ExpiresIn = (int)lifetime.TotalSeconds,
            Interval = (int)interval.TotalSeconds,
        };
    }

    private static TimeSpan ResolveLifetime(
        IReadOnlySettingCollection settings,
        IBackchannelAuthenticationRequest request
    )
    {
        var defaultLifetime = settings.GetValue(OpenIdSettingKeys.CibaRequestLifetime);

        if (
            !string.IsNullOrEmpty(request.RequestedExpiry)
            && int.TryParse(request.RequestedExpiry, out var seconds)
            && seconds > 0
        )
        {
            var requested = TimeSpan.FromSeconds(seconds);
            // Never extend beyond the configured ceiling.
            return requested < defaultLifetime ? requested : defaultLifetime;
        }

        return defaultLifetime;
    }
}
