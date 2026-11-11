// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication;
using NCode.Identity.OpenId.Management;
using NCode.Identity.Registration;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.SharedState;

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
    /// Gets the builder for configuring the shared-state library: the state every replica of a deployment must see, such
    /// as single-use and nonce records. The in-process adapter is registered by default, which is correct only for a
    /// single replica; a deployment that runs several registers a shared adapter on this builder.
    /// </summary>
    IServiceBuilder<SharedStateLibrary> SharedStateLibraryBuilder { get; }

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
    IServiceBuilder<JwtLibrary> JwtLibraryBuilder { get; }

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
