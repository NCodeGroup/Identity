#region Copyright Preamble

// Copyright @ 2023 NCode Group
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
using NCode.Identity.Jose;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication;
using NCode.Identity.OpenId.Management;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Provides extension methods to configure services and handlers for Identity Server.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <summary>
    /// Configures services and handlers for Identity Server.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    /// <param name="configure">The action to configure services for <see cref="IdentityServer"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddIdentityServer(
        this IServiceCollection serviceCollection,
        Action<IServiceBuilder<IdentityServer>> configure
    )
    {
        var builder = ServiceBuilder.Register<IdentityServer>(serviceCollection);
        configure(builder);

        // TODO: Allow configuration of data protection
        serviceCollection.AddDataProtection();

        serviceCollection.AddSecretServices(secretBuilder =>
        {
            secretBuilder.AddPersistenceServices(persistenceBuilder => { }); // TODO
        });

        serviceCollection.AddMediatorServices();
        serviceCollection.AddJoseServices();
        serviceCollection.AddJsonWebTokenServices();

        serviceCollection.AddIdentityServices(identityBuilder =>
        {
            identityBuilder.AddOpenIdCore();
            identityBuilder.AddOpenIdAuthentication();
            identityBuilder.AddOpenIdManagement();
        });

        return serviceCollection;
    }
}
