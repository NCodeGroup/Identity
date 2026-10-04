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
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.Server;

/// <summary>
/// Provides extension methods for <see cref="IIdentityServerBuilder"/>.
/// </summary>
[PublicAPI]
public static class IdentityServerBuilderExtensions
{
    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to configure.</param>
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Binds <see cref="OpenIdOptions"/> and its nested <see cref="TenantResolutionOptions"/> from the specified
        /// <paramref name="configuration"/> section, so a host does not have to repeat the binding boilerplate. When
        /// <paramref name="sectionName"/> is <c>null</c> or empty, <see cref="OpenIdOptions.DefaultSectionName"/> is used.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> to bind the options from.</param>
        /// <param name="sectionName">The configuration section name that holds the OpenID options, or <c>null</c> to use
        /// <see cref="OpenIdOptions.DefaultSectionName"/>.</param>
        /// <returns>The same <see cref="IIdentityServerBuilder"/> instance for method chaining.</returns>
        public IIdentityServerBuilder AddConfiguration(
            IConfiguration configuration,
            string? sectionName = null
        )
        {
            var resolvedSectionName = string.IsNullOrEmpty(sectionName)
                ? OpenIdOptions.DefaultSectionName
                : sectionName;

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.Configure<OpenIdOptions>(
                configuration.GetSection(resolvedSectionName)
            );
            serviceCollection.Configure<OpenIdOptions>(options =>
                options.SectionName = resolvedSectionName
            );

            // Tenant selection is configured separately from tenant materialization; defaults resolve the
            // single "default" tenant when this section is absent.
            serviceCollection.Configure<TenantResolutionOptions>(
                configuration.GetSection($"{resolvedSectionName}:TenantResolution")
            );

            // The bootstrap administrator credential is sourced from its own top-level section so it can be supplied
            // out of band (for example, environment variables); absent it, the feature stays inert (ADR-0044).
            serviceCollection.Configure<BootstrapAdminOptions>(
                configuration.GetSection(BootstrapAdminOptions.DefaultSectionName)
            );

            return builder;
        }
    }
}
