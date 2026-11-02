// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;

namespace NCode.Identity.Bff.Hardening;

/// <summary>
/// Provides the ability to add the BFF's opt-in security headers to the request pipeline.
/// </summary>
[PublicAPI]
public static class BffSecurityHeadersApplicationBuilderExtensions
{
    /// <param name="app">The application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds a middleware that writes <c>Content-Security-Policy</c>, <c>X-Content-Type-Options: nosniff</c>,
        /// <c>Referrer-Policy</c> and <c>Cross-Origin-Opener-Policy</c> on every response, from the
        /// <c>Bff:SecurityHeaders</c> configuration section. A header that the application sets on a response itself
        /// is kept.
        /// </summary>
        /// <remarks>
        /// Call it early, before the static files and the endpoints, so the frontend's own pages carry the headers.
        /// The Content-Security-Policy is the only control that limits a script injected into the frontend, which
        /// would otherwise ride the session cookie.
        /// </remarks>
        /// <returns>The same builder, so that calls can be chained.</returns>
        public IApplicationBuilder UseBffSecurityHeaders()
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseMiddleware<BffSecurityHeadersMiddleware>();
            return app;
        }
    }
}
