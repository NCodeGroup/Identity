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
using Microsoft.Extensions.Options;
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
        /// Configures the base setting-descriptor catalog, the built-in default settings provider, and the validation of
        /// the server's configured settings. The catalog is a composable collection; the authentication slice contributes
        /// its protocol-specific descriptors additively. Setting names are a closed vocabulary, so a configured setting
        /// with no registered descriptor, or a value that does not convert to its type, stops the host when it starts.
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

            // The scopes of the settings cascade are registered data, so an extension adds a scope for an entity of its
            // own beneath the scope it inherits from (ADR-0149, ADR-0150).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<SettingScopeDescriptor>,
                    DefaultSettingScopeDataSource
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IDefaultSettingsProvider, DefaultSettingsProvider>()
            );

            // The server's configured settings are a closed vocabulary typed by the descriptor catalog; a configured
            // setting that cannot be honored stops the host when it starts (ADR-0137, ADR-0139).
            serviceCollection.TryAddSingleton<
                ISettingConfigurationReader,
                DefaultSettingConfigurationReader
            >();
            serviceCollection.AddOptions<OpenIdOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<OpenIdOptions>,
                    ServerSettingsOptionsValidator
                >()
            );

            // The security-profile catalog resolves the registered profiles (a strategy collection contributed by the
            // slices that define them) into their ceiling settings and request-time requirements.
            serviceCollection.TryAddSingleton<
                ISecurityProfileCatalog,
                DefaultSecurityProfileCatalog
            >();

            // The effective-settings resolver is the one place that resolves a scope's effective settings outside the
            // live providers; a host can replace it (ADR-0204).
            serviceCollection.TryAddSingleton<
                IEffectiveSettingsResolver,
                DefaultEffectiveSettingsResolver
            >();

            // Each built-in kind is its own class: TryAddEnumerable keeps one registration per implementation type
            // (ADR-0203).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRedirectUriKind, HttpsRedirectUriKind>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRedirectUriKind, LoopbackRedirectUriKind>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRedirectUriKind, HttpRedirectUriKind>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IRedirectUriKind, PrivateUseRedirectUriKind>()
            );

            // The redirect URI kind catalog resolves the registered kinds by name and judges a redirect URI against the
            // redirect_uri_kinds_supported setting.
            serviceCollection.TryAddSingleton<
                IRedirectUriKindCatalog,
                DefaultRedirectUriKindCatalog
            >();

            return builder;
        }
    }
}
