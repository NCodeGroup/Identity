// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Blazor.Client.Options;
using NCode.Registration;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// Provides extension methods to compose the BFF's Blazor WebAssembly client.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to configure services.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the services of the BFF's Blazor WebAssembly client: an
        /// <see cref="AuthenticationStateProvider"/> seeded from the user document the server persisted, or from a
        /// probe of the BFF's user endpoint, that follows the session, and the options that configure it. A
        /// WebAssembly client never receives a token. The host adds
        /// <see cref="BffBlazorClientHttpClientBuilderExtensions.AddBffAntiforgeryHeader"/> to the
        /// <see cref="HttpClient"/> of each of its calls to the frontend's BFF APIs.
        /// </summary>
        /// <remarks>
        /// Call it only in the WebAssembly project. It keeps the browser's one user in a singleton, so a server host
        /// composes <c>AddBffBlazorLibrary()</c> instead, whose <see cref="AuthenticationStateProvider"/> also serves
        /// prerendering. It removes any other <see cref="AuthenticationStateProvider"/> registration, so the BFF
        /// session is the only source of the user. A WebAssembly host runs no startup validation, so the
        /// <see cref="BffBlazorClientOptions"/> are validated when the session is first created.
        /// </remarks>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<BffBlazorClientLibrary> AddBffBlazorClient()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            var builder = serviceCollection.NewBuilder<BffBlazorClientLibrary>();

            serviceCollection.TryAddSingleton(TimeProvider.System);

            serviceCollection.AddOptions<BffBlazorClientOptions>();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<BffBlazorClientOptions>,
                    BffBlazorClientOptionsValidator
                >()
            );

            serviceCollection.AddAuthorizationCore();
            serviceCollection.AddCascadingAuthenticationState();
            serviceCollection.AddHttpClient(BffBlazorClientDefaults.HttpClientName);

            serviceCollection.TryAddSingleton<BffClientSession>();
            serviceCollection.TryAddTransient<BffAntiforgeryHeaderHandler>();

            serviceCollection.RemoveAll<AuthenticationStateProvider>();
            serviceCollection.AddScoped<
                AuthenticationStateProvider,
                BffClientAuthenticationStateProvider
            >();

            return builder;
        }
    }
}
