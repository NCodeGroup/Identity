// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;

/// <summary>
/// Provides the default implementation of <see cref="ICibaNotificationService"/>. The authentication-device transport
/// is deployment-specific, so <see cref="NotifyAuthenticationDeviceAsync"/> is an inert no-op a host replaces with its
/// real channel (a push provider, a message bus). Client-facing ping and push delivery is handled separately by a
/// <see cref="Delivery.CibaDeliveryJob"/> routed through the <see cref="NCode.Identity.Dispatch.IJobDispatcher"/>.
/// </summary>
internal sealed class DefaultCibaNotificationService : ICibaNotificationService
{
    /// <inheritdoc />
    public ValueTask NotifyAuthenticationDeviceAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant request,
        CancellationToken cancellationToken
    ) => ValueTask.CompletedTask;
}
