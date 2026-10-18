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

using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Delivery;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Ciba;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;

/// <summary>
/// Provides a default implementation of <see cref="ICibaRequestService"/> that records the out-of-band approve/deny
/// decision on the pending CIBA grant and, for ping or push delivery, dispatches a durable, retryable
/// <see cref="CibaDeliveryJob"/> that notifies the client (ping) or delivers the issued tokens (push).
/// </summary>
internal sealed class DefaultCibaRequestService(
    IPersistedGrantService persistedGrantService,
    IJobDispatcher jobDispatcher,
    ICibaTokenResponseService tokenResponseService
) : ICibaRequestService
{
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IJobDispatcher JobDispatcher { get; } = jobDispatcher;
    private ICibaTokenResponseService TokenResponseService { get; } = tokenResponseService;

    /// <inheritdoc />
    public async ValueTask<bool> ApproveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken
    )
    {
        var pendingOrNull = await GetPendingAsync(openIdContext, authReqId, cancellationToken);
        if (!pendingOrNull.HasValue)
            return false;

        var payload = pendingOrNull.Value with
        {
            Status = DeviceAuthorizationStatus.Approved,
            SubjectAuthentication = subjectAuthentication,
        };

        var grantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Ciba,
            authReqId
        );

        var updated = await PersistedGrantService.UpdatePayloadAsync(
            openIdContext,
            grantId,
            payload,
            cancellationToken
        );

        if (!updated)
            return false;

        await DeliverToClientAsync(
            openIdContext,
            openIdClient,
            authReqId,
            payload,
            cancellationToken
        );

        return true;
    }

    // Ping and push delivery to the client's notification endpoint are dispatched as a durable, retryable job so a
    // failed delivery is retried out of band (and can run on a durable dispatcher adapter); poll delivery no-ops here.
    private async ValueTask DeliverToClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant payload,
        CancellationToken cancellationToken
    )
    {
        var isPing = string.Equals(
            payload.DeliveryMode,
            OpenIdConstants.BackchannelTokenDeliveryModes.Ping,
            StringComparison.Ordinal
        );
        var isPush = string.Equals(
            payload.DeliveryMode,
            OpenIdConstants.BackchannelTokenDeliveryModes.Push,
            StringComparison.Ordinal
        );
        if (!isPing && !isPush)
            return;

        var endpoint = openIdClient.Settings.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint);
        if (string.IsNullOrEmpty(endpoint))
            return;

        var job = new CibaDeliveryJob
        {
            DeliveryMode = payload.DeliveryMode,
            ClientId = openIdClient.ClientId,
            EndpointUrl = endpoint,
            ClientNotificationToken = payload.ClientNotificationToken,
            AuthReqId = authReqId,
        };

        if (isPush)
        {
            // Issue the tokens now; a push-mode client never polls the token endpoint (CIBA §10.3). Push delivery has
            // no token-endpoint request, so there is no proof of possession to bind — the tokens are plain bearer.
            var tokenResponse = await TokenResponseService.CreateTokenResponseAsync(
                openIdContext,
                openIdClient,
                payload,
                ProofOfPossessionBinding.Unbound,
                cancellationToken
            );

            job = job with
            {
                AccessToken = tokenResponse.AccessToken,
                TokenType = tokenResponse.TokenType,
                ExpiresInSeconds = tokenResponse.ExpiresIn is { } expiresIn
                    ? (long)expiresIn.TotalSeconds
                    : null,
                RefreshToken = tokenResponse.RefreshToken,
                IdToken = tokenResponse.IdToken,
                Scope = tokenResponse.Scopes is { Count: > 0 } scopes
                    ? string.Join(' ', scopes)
                    : null,
            };
        }

        await JobDispatcher.DispatchAsync(job, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<bool> DenyAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CancellationToken cancellationToken
    )
    {
        var pendingOrNull = await GetPendingAsync(openIdContext, authReqId, cancellationToken);
        if (!pendingOrNull.HasValue)
            return false;

        var grantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Ciba,
            authReqId
        );

        return await PersistedGrantService.UpdatePayloadAsync(
            openIdContext,
            grantId,
            pendingOrNull.Value with
            {
                Status = DeviceAuthorizationStatus.Denied,
            },
            cancellationToken
        );
    }

    private async ValueTask<CibaGrant?> GetPendingAsync(
        OpenIdContext openIdContext,
        string authReqId,
        CancellationToken cancellationToken
    )
    {
        var grantId = PersistedGrantService.CreateGrantId(
            openIdContext.Tenant.TenantId,
            OpenIdConstants.PersistedGrantTypes.Ciba,
            authReqId
        );

        var persistedGrantOrNull = await PersistedGrantService.GetOrDefaultAsync<CibaGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (persistedGrantOrNull is not { Status: PersistedGrantStatus.Active } persistedGrant)
            return null;

        return persistedGrant.Payload.Status == DeviceAuthorizationStatus.Pending
            ? persistedGrant.Payload
            : null;
    }
}
