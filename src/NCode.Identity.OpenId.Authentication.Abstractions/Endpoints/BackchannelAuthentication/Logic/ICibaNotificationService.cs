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
/// Notifies out-of-band channels during a CIBA flow: the end user's authentication device that an approval is pending,
/// and (ping delivery) the client's <c>client_notification_endpoint</c> that the request has completed. The transport
/// is deployment-specific (a push provider, a message bus), so this is an injectable seam; the default implementation
/// is a best-effort no-op. Mirrors the back-channel-logout notifier seam.
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

    /// <summary>
    /// Notifies the client's <c>client_notification_endpoint</c> that a CIBA request has completed (ping delivery).
    /// Best-effort: a delivery failure does not fail the approval.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that initiated the request.</param>
    /// <param name="authReqId">The identifier of the completed request.</param>
    /// <param name="request">The completed request payload.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask NotifyClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CibaGrant request,
        CancellationToken cancellationToken
    );
}
