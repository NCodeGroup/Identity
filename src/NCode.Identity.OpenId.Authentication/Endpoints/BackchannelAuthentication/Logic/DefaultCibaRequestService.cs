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

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Logic;

/// <summary>
/// Provides a default implementation of <see cref="ICibaRequestService"/> that records the out-of-band approve/deny
/// decision on the pending CIBA grant and, for ping delivery, notifies the client that the request completed.
/// </summary>
internal sealed class DefaultCibaRequestService(
    IPersistedGrantService persistedGrantService,
    ICibaNotificationService notificationService
) : ICibaRequestService
{
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private ICibaNotificationService NotificationService { get; } = notificationService;

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

        // Ping delivery: let the client know it can now poll the token endpoint for its tokens.
        if (
            string.Equals(
                payload.DeliveryMode,
                OpenIdConstants.BackchannelTokenDeliveryModes.Ping,
                StringComparison.Ordinal
            )
        )
        {
            await NotificationService.NotifyClientAsync(
                openIdContext,
                openIdClient,
                authReqId,
                payload,
                cancellationToken
            );
        }

        return true;
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
