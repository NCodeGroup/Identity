// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Builds the set of OpenID Connect Front-Channel Logout 1.0 iframe URIs for the relying parties that participated in a
/// terminating session. The OP loads each URI in a hidden iframe so the client can clear its own session in the user
/// agent. Front-Channel Logout is available but disabled by default (it depends on third-party cookies the browser
/// platform is removing); the <c>frontchannel_logout_supported</c> setting gates its use.
/// </summary>
[PublicAPI]
public interface IFrontChannelLogoutService
{
    /// <summary>
    /// Resolves the front-channel logout URIs for the participating clients that have a <c>frontchannel_logout_uri</c>
    /// configured, appending the <c>iss</c> and <c>sid</c> query parameters for clients whose
    /// <c>frontchannel_logout_session_required</c> setting is enabled.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="subject">The <see cref="EndSessionSubject"/> describing the terminated session and its
    /// participating clients.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The front-channel logout URIs to load in hidden iframes; empty when none are configured.</returns>
    ValueTask<IReadOnlyList<string>> GetLogoutUrisAsync(
        OpenIdContext openIdContext,
        EndSessionSubject subject,
        CancellationToken cancellationToken
    );
}
