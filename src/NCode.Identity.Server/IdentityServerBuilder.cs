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
using NCode.PropertyBag;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Provides a builder for configuring the NCode Identity Server and its constituent libraries.
/// </summary>
[PublicAPI]
public interface IIdentityServerBuilder : IServiceBuilder<IdentityServer>
{
    /// <summary>
    /// Gets the builder for configuring data protection services.
    /// </summary>
    IDataProtectionBuilder DataProtectionBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the secrets library.
    /// </summary>
    IServiceBuilder<SecretsLibrary> SecretsLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the secret persistence library.
    /// </summary>
    IServiceBuilder<SecretPersistenceLibrary> SecretPersistenceLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the JOSE library.
    /// </summary>
    IServiceBuilder<JoseLibrary> JoseLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the JSON Web Tokens library.
    /// </summary>
    IServiceBuilder<JsonWebTokensLibrary> JsonWebTokensLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the identity library.
    /// </summary>
    IServiceBuilder<IdentityLibrary> IdentityLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the OpenID core library.
    /// </summary>
    IServiceBuilder<OpenIdCoreLibrary> OpenIdCoreLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the OpenID authentication library.
    /// </summary>
    IServiceBuilder<OpenIdAuthenticationLibrary> OpenIdAuthenticationLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the OpenID management library.
    /// </summary>
    IServiceBuilder<OpenIdManagementLibrary> OpenIdManagementLibraryBuilder { get; }
}

/// <summary>
/// Provides the default implementation of <see cref="IIdentityServerBuilder"/>.
/// </summary>
public sealed class IdentityServerBuilder : ServiceBuilder<IdentityServer>, IIdentityServerBuilder
{
    /// <inheritdoc />
    public IDataProtectionBuilder DataProtectionBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<SecretsLibrary> SecretsLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<SecretPersistenceLibrary> SecretPersistenceLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<JoseLibrary> JoseLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<JsonWebTokensLibrary> JsonWebTokensLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<IdentityLibrary> IdentityLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdCoreLibrary> OpenIdCoreLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdAuthenticationLibrary> OpenIdAuthenticationLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdManagementLibrary> OpenIdManagementLibraryBuilder { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityServerBuilder"/> class.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    public IdentityServerBuilder(IServiceCollection serviceCollection)
        : base(serviceCollection)
    {
        serviceCollection.AddPropertyBag();

        DataProtectionBuilder = serviceCollection.AddDataProtection();
        SecretsLibraryBuilder = serviceCollection.AddSecretsLibrary();
        SecretPersistenceLibraryBuilder = SecretsLibraryBuilder.AddPersistenceLibrary();

        JoseLibraryBuilder = serviceCollection.AddJoseLibrary();
        JsonWebTokensLibraryBuilder = serviceCollection.AddJsonWebTokensLibrary();

        IdentityLibraryBuilder = serviceCollection.AddIdentityLibrary();
        OpenIdCoreLibraryBuilder = IdentityLibraryBuilder.AddOpenIdCoreLibrary();
        OpenIdAuthenticationLibraryBuilder =
            IdentityLibraryBuilder.AddOpenIdAuthenticationLibrary();
        OpenIdManagementLibraryBuilder = IdentityLibraryBuilder.AddOpenIdManagementLibrary();
    }
}
