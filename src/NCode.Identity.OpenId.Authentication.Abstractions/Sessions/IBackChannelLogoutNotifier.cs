// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Delivers OpenID Connect Back-Channel Logout notifications: it builds a signed <c>logout_token</c> and sends it,
/// server-to-server, to a relying party's registered <c>backchannel_logout_uri</c>. The transport is an abstraction
/// because delivery policy (retries, fan-out, out-of-band queuing) is deployment-specific.
/// </summary>
[PublicAPI]
public interface IBackChannelLogoutNotifier
{
    /// <summary>
    /// Notifies the specified client that a session it participated in has been terminated. Implementations are
    /// best-effort: a client with no <c>backchannel_logout_uri</c> configured, or a delivery failure, does not fail
    /// the logout operation.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The relying party to notify.</param>
    /// <param name="subjectId">The identifier of the subject being logged out, or <see langword="null"/>.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>) being terminated, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask NotifyAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        string? sessionId,
        CancellationToken cancellationToken
    );
}
