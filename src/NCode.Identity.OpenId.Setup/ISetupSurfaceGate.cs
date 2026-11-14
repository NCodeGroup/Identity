// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Decides whether the setup surface, its page, its assets and its API, is served for a request. Every setup route
/// asks the same gate, so the surface exists for all of them or for none.
/// </summary>
internal interface ISetupSurfaceGate
{
    /// <summary>
    /// Determines whether the setup surface is served for the request: the instance is not initialized, and the request
    /// is HTTPS (unless <c>LocalDevelopment:Enabled</c> admits a loopback request).
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> when the surface is served; otherwise, <c>false</c>, and the route answers <c>404</c>.</returns>
    ValueTask<bool> IsServedAsync(HttpContext httpContext, CancellationToken cancellationToken);
}
