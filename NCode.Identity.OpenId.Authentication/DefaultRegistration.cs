#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Contexts;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.Servers;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tenants;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Authentication.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="IdentityLibrary"/>.</param>
    extension(IServiceBuilder<IdentityLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Authentication.
        /// </summary>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddOpenIdAuthenticationLibrary()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddMediator();

            serviceCollection.VerifyIsRegistered<JoseLibrary>();

            var newBuilder = builder.NewBuilder<OpenIdAuthenticationLibrary>();

            newBuilder
                .AddClientServices()
                .AddContextServices()
                .AddEndpoints()
                .AddLogicServices()
                .AddMessageServices()
                .AddServerServices()
                .AddSettingServices()
                .AddSubjectServices()
                .AddTenantServices()
                .AddTokenServices();

            return newBuilder;
        }
    }
}
