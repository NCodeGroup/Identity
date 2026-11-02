// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Defines the catalogued event names (<c>event_name</c>) of the OpenID protocol plane's audit events.
/// </summary>
[PublicAPI]
public static class OpenIdAuditEventNames
{
    /// <summary>
    /// A security token was issued.
    /// </summary>
    public const string TokenIssued = "token.issued";

    /// <summary>
    /// A token (a refresh-token grant) was revoked.
    /// </summary>
    public const string TokenRevoked = "token.revoked";

    /// <summary>
    /// A refresh token was presented whose grant had already been revoked (a reuse/replay signal).
    /// </summary>
    public const string RefreshTokenReplay = "token.refresh.replay";

    /// <summary>
    /// A rotated refresh token was presented again within its reuse interval and answered as a retry of its
    /// rotation.
    /// </summary>
    public const string RefreshTokenRetry = "token.refresh.retry";

    /// <summary>
    /// An authorization request was granted for a subject.
    /// </summary>
    public const string AuthorizationGranted = "authorization.granted";

    /// <summary>
    /// An authorization request was denied for a subject.
    /// </summary>
    public const string AuthorizationDenied = "authorization.denied";

    /// <summary>
    /// Authenticating a client at the token endpoint failed.
    /// </summary>
    public const string ClientAuthentication = "client.authentication";

    /// <summary>
    /// The server authenticated (or failed to authenticate) an end-user subject for a protocol flow.
    /// </summary>
    public const string SubjectAuthentication = "subject.authentication";

    /// <summary>
    /// A protocol endpoint returned an <c>OAuth</c> or <c>OpenID Connect</c> error response.
    /// </summary>
    public const string Error = "openid.error";

    /// <summary>
    /// A server-side end-user session was established at sign-in.
    /// </summary>
    public const string SessionCreated = "session.created";

    /// <summary>
    /// A server-side end-user session (or every session a subject holds) was terminated.
    /// </summary>
    public const string SessionTerminated = "session.terminated";
}
