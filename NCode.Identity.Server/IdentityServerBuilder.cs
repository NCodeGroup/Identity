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
using Microsoft.AspNetCore.DataProtection;
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

[PublicAPI]
public interface IIdentityServerBuilder : IServiceBuilder<IdentityServer>
{
    IDataProtectionBuilder DataProtectionBuilder { get; }

    IServiceBuilder<SecretsLibrary> SecretsLibraryBuilder { get; }

    IServiceBuilder<SecretPersistenceLibrary> SecretPersistenceLibraryBuilder { get; }

    IServiceBuilder<IdentityLibrary> IdentityLibraryBuilder { get; }

    IServiceBuilder<OpenIdCoreLibrary> OpenIdCoreLibraryBuilder { get; }

    IServiceBuilder<OpenIdAuthenticationLibrary> OpenIdAuthenticationLibraryBuilder { get; }

    IServiceBuilder<OpenIdManagementLibrary> OpenIdManagementLibraryBuilder { get; }
}

public sealed class IdentityServerBuilder : ServiceBuilder<IdentityServer>, IIdentityServerBuilder
{
    /// <inheritdoc />
    public IDataProtectionBuilder DataProtectionBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<SecretsLibrary> SecretsLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<SecretPersistenceLibrary> SecretPersistenceLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<IdentityLibrary> IdentityLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdCoreLibrary> OpenIdCoreLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdAuthenticationLibrary> OpenIdAuthenticationLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdManagementLibrary> OpenIdManagementLibraryBuilder { get; }

    public IdentityServerBuilder(IServiceCollection serviceCollection)
        : base(serviceCollection)
    {
        DataProtectionBuilder = serviceCollection.AddDataProtection();
        SecretsLibraryBuilder = serviceCollection.AddSecretsLibrary();
        SecretPersistenceLibraryBuilder = SecretsLibraryBuilder.AddSecretPersistence();

        serviceCollection.AddMediatorServices();
        serviceCollection.AddJoseServices();
        serviceCollection.AddJsonWebTokenServices();

        IdentityLibraryBuilder = serviceCollection.AddIdentityLibrary();
        OpenIdCoreLibraryBuilder = IdentityLibraryBuilder.AddOpenIdCore();
        OpenIdAuthenticationLibraryBuilder = IdentityLibraryBuilder.AddOpenIdAuthentication();
        OpenIdManagementLibraryBuilder = IdentityLibraryBuilder.AddOpenIdManagement();
    }
}
