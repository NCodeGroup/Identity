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
using NCode.Collections.Providers;
using NCode.Identity.Settings;
using NCode.Registration;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides extension methods to configure the base OpenID setting-descriptor catalog and default settings.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdCoreLibrary> builder)
    {
        /// <summary>
        /// Configures the base setting-descriptor catalog and the built-in default settings provider. The catalog is a
        /// composable collection; the authentication slice contributes its protocol-specific descriptors additively.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdCoreLibrary> AddSettingServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<SettingDescriptor>,
                    DefaultSettingDescriptorDataSource
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IDefaultSettingsProvider, DefaultSettingsProvider>()
            );

            return builder;
        }
    }
}
