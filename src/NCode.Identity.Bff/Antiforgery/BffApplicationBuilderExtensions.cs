// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;

namespace NCode.Identity.Bff.Antiforgery;

/// <summary>
/// Provides the ability to add the backend-for-frontend (BFF) middleware to the request pipeline.
/// </summary>
[PublicAPI]
public static class BffApplicationBuilderExtensions
{
    /// <param name="app">The application builder.</param>
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the BFF middleware, which applies the anti-forgery and Fetch Metadata checks and answers
        /// session store and access token failures with problem responses.
        /// </summary>
        /// <remarks>
        /// Call it in this order: CORS, <c>UseBff()</c>, <c>UseAuthentication()</c>, <c>UseAuthorization()</c>.
        /// It throws when <c>UseAuthentication()</c> ran before it.
        /// </remarks>
        /// <returns>The same builder, so that calls can be chained.</returns>
        public IApplicationBuilder UseBff()
        {
            ArgumentNullException.ThrowIfNull(app);

            app.UseMiddleware<BffMiddleware>();
            return app;
        }
    }
}
