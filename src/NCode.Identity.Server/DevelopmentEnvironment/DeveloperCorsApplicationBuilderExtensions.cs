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
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace NCode.Identity.Server;

/// <summary>
/// Provides the request-pipeline counterpart to <see cref="DeveloperCorsRegistration"/>: applies the development CORS
/// policy registered by <c>AddDeveloperCors</c>.
/// </summary>
[PublicAPI]
public static class DeveloperCorsApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Adds the CORS middleware using the development policy (<see cref="DeveloperCorsRegistration.PolicyName"/>).
        /// Call after <c>UseRouting</c> and before the authentication/authorization and endpoint middleware. It is a
        /// <b>no-op</b> when <c>AddDeveloperCors</c> has not registered the policy — as in Production, where
        /// <c>AddDevelopmentEnvironment</c> registers nothing — so it is safe to leave wired.
        /// </summary>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseDeveloperCors()
        {
            ArgumentNullException.ThrowIfNull(app);

            var corsOptions = app
                .ApplicationServices.GetRequiredService<IOptions<CorsOptions>>()
                .Value;
            var policy = corsOptions.GetPolicy(DeveloperCorsRegistration.PolicyName);

            return policy is null ? app : app.UseCors(DeveloperCorsRegistration.PolicyName);
        }
    }
}
