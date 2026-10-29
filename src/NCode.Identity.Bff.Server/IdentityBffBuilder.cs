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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.Events;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets;
using NCode.Identity.SharedState;
using NCode.Registration;

namespace NCode.Identity.Bff.Server;

/// <summary>
/// Provides the default implementation of <see cref="IIdentityBffBuilder"/>: the composition root that references the
/// implementation packages a BFF needs and wires them together.
/// </summary>
// See ADR-0016.
internal sealed class IdentityBffBuilder : ServiceBuilder<IdentityBff>, IIdentityBffBuilder
{
    /// <inheritdoc />
    public IServiceBuilder<SharedStateLibrary> SharedStateLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<EventsLibrary> EventsLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<SecretsLibrary> SecretsLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<JoseLibrary> JoseLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<JwtLibrary> JwtLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdClientLibrary> OpenIdClientLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<BffLibrary> BffLibraryBuilder { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityBffBuilder"/> class.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">The configuration whose <c>Bff</c> section configures the BFF.</param>
    public IdentityBffBuilder(IServiceCollection serviceCollection, IConfiguration configuration)
        : base(serviceCollection)
    {
        // The in-process adapters are the defaults, correct for a single replica; a deployment that runs several swaps
        // in shared adapters (AddValkey) on SharedStateLibraryBuilder, or registers its own first.
        SharedStateLibraryBuilder = serviceCollection
            .AddSharedStateServices()
            .AddInProcessExpiringKeySet()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();

        EventsLibraryBuilder = serviceCollection.AddEventServices();

        SecretsLibraryBuilder = serviceCollection.AddSecretsLibrary();
        JoseLibraryBuilder = SecretsLibraryBuilder.AddJoseLibrary();
        JwtLibraryBuilder = JoseLibraryBuilder.AddJwtLibrary();
        OpenIdClientLibraryBuilder = JwtLibraryBuilder.AddOpenIdClientLibrary();

        BffLibraryBuilder = OpenIdClientLibraryBuilder
            .AddBffLibrary(EventsLibraryBuilder)
            .AddBffConfiguration(configuration);

        // Only the composition root knows both the BFF and the protocol client, so it checks the lease against the
        // client's request timeout (ADR-0291).
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<BffOptions>, BffRefreshLeaseValidator>()
        );
    }
}
