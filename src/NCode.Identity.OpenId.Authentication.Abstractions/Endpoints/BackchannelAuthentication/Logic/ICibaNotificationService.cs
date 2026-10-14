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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Logic;

/// <summary>
/// Notifies the end user's authentication device that a CIBA request is awaiting their approval. The transport (a push
/// provider, a message bus) is deployment-specific, so the default implementation is an inert no-op a host replaces.
/// Delivery of the completion ping and the pushed tokens to the client's <c>client_notification_endpoint</c> is a
/// separate concern handled out of band by the default implementation (a retryable background dispatch job), so it is
/// retried and can run on a durable dispatcher adapter.
/// </summary>
[PublicAPI]
public interface ICibaNotificationService
{
    /// <summary>
    /// Notifies the end user's authentication device that a CIBA request is pending their approval. Best-effort: a
    /// delivery failure does not fail the backchannel authentication request.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that initiated the request.</param>
    /// <param name="authReqId">The identifier of the pending request.</param>
    /// <param name="request">The pending request payload.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask NotifyAuthenticationDeviceAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant request,
        CancellationToken cancellationToken
    );
}
