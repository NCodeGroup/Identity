// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration;

namespace NCode.Identity.Bff.Blazor;

/// <summary>
/// Provides extension methods to compose the BFF's Blazor server library.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> of the <see cref="BffLibrary"/>.</param>
    extension(IServiceBuilder<BffLibrary> builder)
    {
        /// <summary>
        /// Registers the services of the BFF's Blazor server library: an <see cref="AuthenticationStateProvider"/>
        /// that revalidates a circuit's connection session and persists the user document for a WebAssembly client, and
        /// a <see cref="CircuitHandler"/> that makes the circuit's connection current for its inbound activity so that
        /// the circuit's access token handlers obtain tokens without an <c>HttpContext</c>. The host also calls
        /// <c>AddRazorComponents()</c> and <c>AddCascadingAuthenticationState()</c> on its services, maps its
        /// components with <c>MapRazorComponents&lt;App&gt;()</c> followed by
        /// <see cref="BffRazorComponentsEndpointConventionBuilderExtensions"/>'s <c>AsBffComponents()</c>, and adds
        /// <c>UseBff()</c> to the pipeline before <c>UseAuthentication()</c>, <c>UseAuthorization()</c> and
        /// <c>UseAntiforgery()</c>.
        /// </summary>
        /// <remarks>
        /// It removes any other <see cref="AuthenticationStateProvider"/> registration, such as the one
        /// <c>AddRazorComponents()</c> adds, whichever runs first, so the BFF session is the circuit's only source of
        /// identity.
        /// </remarks>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<BffBlazorLibrary> AddBffBlazorLibrary()
        {
            ArgumentNullException.ThrowIfNull(builder);

            var newBuilder = builder.NewBuilder<BffBlazorLibrary>();
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddHttpContextAccessor();

            serviceCollection.RemoveAll<AuthenticationStateProvider>();
            serviceCollection.AddScoped<
                AuthenticationStateProvider,
                BffAuthenticationStateProvider
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Scoped<CircuitHandler, BffCircuitHandler>()
            );

            return newBuilder;
        }
    }
}
