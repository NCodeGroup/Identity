#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Collections.Providers;
using NCode.Registration;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides extension methods to configure services and handlers for Identity Secrets.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Configures services and handlers for Identity Secrets.
        /// </summary>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddSecretServices()
        {
            return serviceCollection.AddSecretServices(_ => { });
        }

        /// <summary>
        /// Configures services and handlers for Identity Secrets.
        /// </summary>
        /// <param name="configure">The action to configure services for <see cref="SecretsLibrary"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
        public IServiceCollection AddSecretServices(Action<IServiceBuilder<SecretsLibrary>> configure)
        {
            var builder = ServiceBuilder.Register<SecretsLibrary>(serviceCollection);
            configure(builder);

            serviceCollection.AddCollectionProviders();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionProvider,
                DefaultSecretKeyCollectionProvider>();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionProviderFactory,
                DefaultSecretKeyCollectionProviderFactory>();

            serviceCollection.TryAddSingleton<
                ISecretKeyFactory,
                DefaultSecretKeyFactory>();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionFactory,
                DefaultSecretKeyCollectionFactory>();

            return serviceCollection;
        }
    }
}
