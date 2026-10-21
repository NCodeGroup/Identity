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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Endpoints;

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
        /// <c>AddDevelopmentEnvironment</c> registers nothing — so it is safe to leave wired. The policy never applies
        /// to a front-channel endpoint (<see cref="IOpenIdEndpointFrontChannelMetadata"/>): the authorization,
        /// continue, and end-session endpoints and the interactive surface answer no cross-origin request or CORS
        /// preflight with CORS headers (FAPI 2.0 Security Profile §5.3.2.1).
        /// </summary>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseDeveloperCors()
        {
            ArgumentNullException.ThrowIfNull(app);

            var corsOptions = app
                .ApplicationServices.GetRequiredService<IOptions<CorsOptions>>()
                .Value;
            var policy = corsOptions.GetPolicy(DeveloperCorsRegistration.PolicyName);

            // The middleware is branched rather than the endpoints marked with DisableCors: CORS metadata on an endpoint
            // fails every request a host without CORS middleware serves (ADR-0109).
            return policy is null
                ? app
                : app.UseWhen(
                    context => !context.GetEndpoint().IsOpenIdFrontChannel,
                    branch => branch.UseCors(DeveloperCorsRegistration.PolicyName)
                );
        }
    }
}
