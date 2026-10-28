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
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;

/// <summary>
/// Provides a default implementation of <see cref="ICibaRequestService"/> that records the out-of-band approve/deny
/// decision on the pending CIBA grant and, for ping or push delivery, dispatches a durable, retryable
/// <see cref="CibaDeliveryJob"/> that notifies the client (ping), delivers the issued tokens (push), or tells the
/// client its request was denied.
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

        // The approving subject is validated before the approval is recorded: push delivery issues the tokens at
        // approval, and a refused approval denies the request. See ADR-0225.
        if (
            !await IsSubjectValidAsync(
                openIdContext,
                openIdClient,
                subjectAuthentication,
                cancellationToken
            )
        )
        {
            await DenyAsync(openIdContext, openIdClient, authReqId, cancellationToken);
            return false;
        }

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

        // Only the call that decides a pending request delivers, so a concurrent approval issues no second set of
        // tokens. See ADR-0222.
        var updated = await PersistedGrantService.UpdatePayloadAsync<CibaGrant>(
            openIdContext,
            grantId,
            stored =>
                stored.Payload.Status == DeviceAuthorizationStatus.Pending
                    ? new PersistedGrantUpdate<CibaGrant>
                    {
                        Payload = stored.Payload with
                        {
                            Status = DeviceAuthorizationStatus.Approved,
                            SubjectAuthentication = subjectAuthentication,
                        },
                    }
                    : null,
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
        if (CreateDeliveryJob(openIdClient, authReqId, payload) is not { } job)
            return;

        if (
            string.Equals(
                job.DeliveryMode,
                OpenIdConstants.BackchannelTokenDeliveryModes.Push,
                StringComparison.Ordinal
            )
        )
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

    // A denial reaches a ping or push client so it does not wait for a request that can no longer be approved. A ping
    // tells the client to poll, and the token endpoint answers access_denied; a push carries the error payload (CIBA
    // Core §10.2, §12). See ADR-0231.
    private async ValueTask NotifyDeniedAsync(
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant payload,
        CancellationToken cancellationToken
    )
    {
        if (CreateDeliveryJob(openIdClient, authReqId, payload) is not { } job)
            return;

        if (
            string.Equals(
                job.DeliveryMode,
                OpenIdConstants.BackchannelTokenDeliveryModes.Push,
                StringComparison.Ordinal
            )
        )
        {
            job = job with { Error = OpenIdProtocol.ErrorCodes.AccessDenied };
        }

        await JobDispatcher.DispatchAsync(job, cancellationToken: cancellationToken);
    }

    private static CibaDeliveryJob? CreateDeliveryJob(
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant payload
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
            return null;

        var endpoint = openIdClient.Settings.GetValue(OpenIdSettingKeys.ClientNotificationEndpoint);
        if (string.IsNullOrEmpty(endpoint))
            return null;

        return new CibaDeliveryJob
        {
            DeliveryMode = payload.DeliveryMode,
            ClientId = openIdClient.ClientId,
            EndpointUrl = endpoint,
            ClientNotificationToken = payload.ClientNotificationToken,
            AuthReqId = authReqId,
        };
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

        var updated = await PersistedGrantService.UpdatePayloadAsync<CibaGrant>(
            openIdContext,
            grantId,
            stored =>
                stored.Payload.Status == DeviceAuthorizationStatus.Pending
                    ? new PersistedGrantUpdate<CibaGrant>
                    {
                        Payload = stored.Payload with { Status = DeviceAuthorizationStatus.Denied },
                    }
                    : null,
            cancellationToken
        );

        if (!updated)
            return false;

        // Only the call that decides a pending request notifies, so a concurrent decision sends no second
        // notification. See ADR-0231.
        await NotifyDeniedAsync(openIdClient, authReqId, pendingOrNull.Value, cancellationToken);

        return true;
    }

    private static async ValueTask<bool> IsSubjectValidAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken
    )
    {
        var operationDisposition = new OperationDisposition<IOpenIdError>();

        // The approval happens outside a protocol request, so there is no request to validate against, and the
        // authentication age the authorization request enforced is not checked again.
        await openIdContext.Mediator.SendAsync(
            new ValidateSubjectAuthenticationCommand(
                openIdContext,
                openIdClient,
                OpenIdRequest: null,
                subjectAuthentication,
                operationDisposition,
                EnforceAuthenticationAge: false
            ),
            cancellationToken
        );

        return !operationDisposition.HasError;
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
