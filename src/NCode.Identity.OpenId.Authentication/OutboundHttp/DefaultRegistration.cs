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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Provides extension methods to configure the shared, SSRF-guarded outbound HTTP services used by every
/// server-initiated fetch of, or delivery to, a client-supplied URL.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the outbound HTTP forgery guard and the capped response-body reader.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddOutboundHttpServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddHttpClient();
            serviceCollection.TryAddSingleton<
                IOutboundHttpForgeryGuard,
                DefaultOutboundHttpForgeryGuard
            >();
            serviceCollection.TryAddSingleton<
                IOutboundHttpContentReader,
                DefaultOutboundHttpContentReader
            >();

            return builder;
        }
    }
}
