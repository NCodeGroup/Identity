// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Claims;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.PrincipalResolution;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.OpenId.Serialization;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Registration;
using NCode.Identity.Registration.AspNetCore;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder"/> to configure OpenId Core services and handlers.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="SettingsLibrary"/> builder this library extends.</param>
    extension(IServiceBuilder<SettingsLibrary> builder)
    {
        /// <summary>
        /// Configures OpenId Core services and handlers.
        /// </summary>
        /// <param name="claimsBuilder">The <see cref="ClaimsLibrary"/> builder — the compile-time proof that the claims
        /// serializer and claims service the core services depend on are registered.</param>
        /// <param name="cryptographyBuilder">The <see cref="CryptographyLibrary"/> builder — the compile-time proof that
        /// the cryptography service the core services depend on is registered.</param>
        public IServiceBuilder<OpenIdCoreLibrary> AddOpenIdCoreLibrary(
            IServiceBuilder<ClaimsLibrary> claimsBuilder,
            IServiceBuilder<CryptographyLibrary> cryptographyBuilder
        )
        {
            ArgumentNullException.ThrowIfNull(claimsBuilder);
            ArgumentNullException.ThrowIfNull(cryptographyBuilder);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.AddEndpointGroupRouting();

            var newBuilder = builder.NewBuilder<OpenIdCoreLibrary>();

            newBuilder
                .AddSerializationServices()
                .AddEnvironmentServices()
                .AddExceptionServices()
                .AddMessageServices()
                .AddResultServices()
                .AddContextServices()
                .AddPlatformServices()
                .AddSettingServices()
                .AddSecretProtectionServices()
                .AddTenantResolutionServices()
                .AddTeamServices()
                .AddPrincipalResolutionServices();

            return newBuilder;
        }
    }
}
