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
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Registration;

namespace NCode.Identity.Secrets;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register services
/// required for managing identity secrets and cryptographic keys.
/// </summary>
/// <remarks>
/// <para>
/// This class provides the entry point for configuring the Identity Secrets library,
/// which manages cryptographic secret keys used for signing, encryption, and other
/// security operations.
/// </para>
/// </remarks>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers all services required for the Identity Secrets library.
        /// </summary>
        /// <returns>An <see cref="IServiceBuilder{T}"/> for <see cref="SecretsLibrary"/> that can be used
        /// to further configure secrets-related services, such as adding persistence support.</returns>
        /// <remarks>
        /// <para>
        /// This method registers the following services:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Data protection services via <c>AddDataProtection()</c>.</description></item>
        /// <item><description><see cref="IDataProtectorFactory{T}"/> - Factory for creating data protectors.</description></item>
        /// <item><description><see cref="IDataProtectorFactory{T}"/> for <see cref="SecretKey"/> - Uses
        /// <see cref="DefaultSecretKeyDataProtectorFactory"/> with ephemeral, process-local keys.</description></item>
        /// <item><description><see cref="ISecretKeyCollectionProvider"/> - Provides access to collections of secret keys.</description></item>
        /// <item><description><see cref="ISecretKeyCollectionProviderFactory"/> - Factory for creating secret key collection providers.</description></item>
        /// <item><description><see cref="ISecretKeyFactory"/> - Factory for creating individual secret keys.</description></item>
        /// <item><description><see cref="ISecretKeyCollectionFactory"/> - Factory for creating secret key collections.</description></item>
        /// </list>
        /// <para>
        /// Services are registered using <c>TryAddSingleton</c> semantics where applicable,
        /// meaning existing registrations will not be overwritten.
        /// </para>
        /// </remarks>
        public IServiceBuilder<SecretsLibrary> AddSecretsLibrary()
        {
            var builder = serviceCollection.NewBuilder<SecretsLibrary>();

            serviceCollection.AddCollectionProviders();

            serviceCollection.AddDataProtection();
            serviceCollection.AddDataProtectorFactory();
            serviceCollection.AddDataProtectorFactory<SecretKey, DefaultSecretKeyDataProtectorFactory>();

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
