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
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Logic;

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
