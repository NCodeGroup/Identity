// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication;

/// <summary>
/// Performs the out-of-band approval transition of a pending CIBA request. CIBA approval happens on the end user's
/// separate authentication device, so there is no OP-hosted approval page; a deployment's authentication-device
/// callback invokes this service to record the decision (and, for ping delivery, trigger the client notification).
/// </summary>
[PublicAPI]
public interface ICibaRequestService
{
    /// <summary>
    /// Approves a pending CIBA request, recording the approving subject. For ping delivery, notifies the client's
    /// <c>client_notification_endpoint</c>. The approving subject must pass the subject validation a token request
    /// applies; a refused subject denies the request.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that initiated the request.</param>
    /// <param name="authReqId">The identifier of the pending request.</param>
    /// <param name="subjectAuthentication">The authenticated subject approving the request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> containing <c>true</c> if a pending request was found and approved; otherwise, <c>false</c>, including when the approving subject is refused.</returns>
    ValueTask<bool> ApproveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        SubjectAuthentication subjectAuthentication,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Denies a pending CIBA request. The denial is delivered to a ping or push client's
    /// <c>client_notification_endpoint</c>: a ping notifies the client, which polls the token endpoint and receives
    /// <c>access_denied</c>, and a push carries the <c>access_denied</c> error payload. Only the call that decides the
    /// request delivers.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> that initiated the request.</param>
    /// <param name="authReqId">The identifier of the pending request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> containing <c>true</c> if a pending request was found and denied; otherwise, <c>false</c>.</returns>
    ValueTask<bool> DenyAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string authReqId,
        CancellationToken cancellationToken
    );
}
