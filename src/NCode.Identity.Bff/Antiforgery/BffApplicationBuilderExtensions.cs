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
