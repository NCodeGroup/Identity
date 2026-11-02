// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Dispatch;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// A self-contained, serializable description of a single OpenID Connect Back-Channel Logout delivery: the signed
/// <c>logout_token</c> to <c>POST</c> to a relying party's registered <c>backchannel_logout_uri</c>. It is handed to
/// the <see cref="NCode.Identity.Dispatch.IJobDispatcher"/> so delivery is retried out of band and can be routed
/// through a durable or distributed dispatcher adapter. Because the signed token is a short-lived bearer artifact, a
/// durable adapter that persists this job should protect the payload at rest.
/// </summary>
internal sealed record BackChannelLogoutDeliveryJob : IDispatchJob
{
    /// <summary>
    /// Gets the identifier of the relying party the notification is delivered to, used for diagnostics.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Gets the relying party's <c>backchannel_logout_uri</c> the <c>logout_token</c> is <c>POST</c>ed to.
    /// </summary>
    public required string LogoutUri { get; init; }

    /// <summary>
    /// Gets the signed <c>logout_token</c> delivered as the form-encoded <c>logout_token</c> parameter.
    /// </summary>
    public required string LogoutToken { get; init; }
}
