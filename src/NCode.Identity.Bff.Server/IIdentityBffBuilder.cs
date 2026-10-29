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
using NCode.Identity.Events;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets;
using NCode.Identity.SharedState;
using NCode.Registration;

namespace NCode.Identity.Bff.Server;

/// <summary>
/// Provides a builder for configuring a backend for frontend (BFF) and the libraries it is composed of.
/// </summary>
[PublicAPI]
public interface IIdentityBffBuilder : IServiceBuilder<IdentityBff>
{
    /// <summary>
    /// Gets the builder for configuring the shared-state library: the sessions, login transactions, refresh leases and
    /// indexes every replica must see. The in-process adapters are registered by default, which is correct only for a
    /// single replica; a deployment that runs several registers a shared adapter, such as Valkey, on this builder.
    /// </summary>
    IServiceBuilder<SharedStateLibrary> SharedStateLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the events library, through which the BFF publishes its audit events.
    /// </summary>
    IServiceBuilder<EventsLibrary> EventsLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the secrets library, which holds the key rings of the BFF's client
    /// authentication and DPoP keys.
    /// </summary>
    IServiceBuilder<SecretsLibrary> SecretsLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the JOSE library.
    /// </summary>
    IServiceBuilder<JoseLibrary> JoseLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the JWT library.
    /// </summary>
    IServiceBuilder<JwtLibrary> JwtLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the OpenID protocol client, through which the BFF talks to its provider.
    /// </summary>
    IServiceBuilder<OpenIdClientLibrary> OpenIdClientLibraryBuilder { get; }

    /// <summary>
    /// Gets the builder for configuring the BFF library itself, such as its login gates, session validators and token
    /// requirements.
    /// </summary>
    IServiceBuilder<BffLibrary> BffLibraryBuilder { get; }
}
