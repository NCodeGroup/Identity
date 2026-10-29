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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.Bff.Server;

/// <summary>
/// Provides extension methods to compose a backend for frontend (BFF).
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Composes a backend for frontend in one call: the shared-state (in-process by default), events, secrets, JOSE,
        /// JWT and OpenID protocol client libraries, and the BFF library configured from the <c>Bff</c> section of
        /// <paramref name="configuration"/>. The host still provides what is unsafe to default: the key rings its
        /// configuration names, and a shared-state adapter when it runs several replicas.
        /// </summary>
        /// <param name="configuration">The configuration whose <c>Bff</c> section configures the BFF.</param>
        /// <returns>The <see cref="IIdentityBffBuilder"/> instance for additional configuration.</returns>
        public IIdentityBffBuilder AddIdentityBff(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configuration);

            return new IdentityBffBuilder(serviceCollection, configuration);
        }
    }
}
