// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
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

namespace NCode.Identity.OpenId;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder"/> to configure OpenId Core services and handlers.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="IdentityLibrary"/>.</param>
    extension(IServiceBuilder<IdentityLibrary> builder)
    {
        /// <summary>
        /// Configures OpenId Core services and handlers.
        /// </summary>
        public IServiceBuilder<OpenIdCoreLibrary> AddOpenIdCoreLibrary()
        {
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
