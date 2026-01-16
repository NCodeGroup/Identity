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
        public IServiceBuilder<SecretsLibrary> AddSecretsLibrary()
        {
            var builder = serviceCollection.NewBuilder<SecretsLibrary>();

            serviceCollection.AddCollectionProviders();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionProvider,
                DefaultSecretKeyCollectionProvider
            >();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionProviderFactory,
                DefaultSecretKeyCollectionProviderFactory
            >();

            serviceCollection.TryAddSingleton<
                ISecretKeyFactory,
                DefaultSecretKeyFactory
            >();

            serviceCollection.TryAddSingleton<
                ISecretKeyCollectionFactory,
                DefaultSecretKeyCollectionFactory
            >();

            return builder;
        }
    }
}
