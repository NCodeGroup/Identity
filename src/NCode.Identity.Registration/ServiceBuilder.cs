// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Identity.Registration;

/// <summary>
/// Provides the ability to configure services using the builder pattern.
/// </summary>
[PublicAPI]
public interface IServiceBuilder
{
    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> that is used to configure services.
    /// </summary>
    IServiceCollection ServiceCollection { get; }

    /// <summary>
    /// Creates a new <see cref="IServiceBuilder{TNewMarker}"/> over the same <see cref="IServiceCollection"/>,
    /// discriminated by the specified marker type.
    /// </summary>
    /// <typeparam name="TNewMarker">The type that discriminates the new service builder.</typeparam>
    /// <returns>The newly created <see cref="IServiceBuilder{TNewMarker}"/> instance.</returns>
    IServiceBuilder<TNewMarker> NewBuilder<TNewMarker>()
        where TNewMarker : IRegistrationMarker<TNewMarker>, new();
}

/// <summary>
/// Provides the ability to configure services using the builder pattern with a type discriminator.
/// </summary>
/// <typeparam name="TMarker">The type that discriminates the service builder.</typeparam>
[PublicAPI]
public interface IServiceBuilder<TMarker> : IServiceBuilder
    where TMarker : IRegistrationMarker<TMarker>, new()
{
    // nothing
}

/// <summary>
/// Provides a default implementation of the <see cref="IServiceBuilder{TMarker}"/> abstraction.
/// </summary>
/// <typeparam name="TMarker">The type that discriminates the service builder.</typeparam>
[PublicAPI]
public class ServiceBuilder<TMarker> : IServiceBuilder<TMarker>
    where TMarker : IRegistrationMarker<TMarker>, new()
{
    /// <inheritdoc/>
    public IServiceCollection ServiceCollection { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceBuilder{TMarker}"/> class and registers the
    /// <typeparamref name="TMarker"/> registration marker in the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> that is used to configure services.</param>
    public ServiceBuilder(IServiceCollection serviceCollection)
    {
        ServiceCollection = serviceCollection;
        serviceCollection.AddRegistrationMarker<TMarker>();
    }

    /// <inheritdoc/>
    public IServiceBuilder<TNewMarker> NewBuilder<TNewMarker>()
        where TNewMarker : IRegistrationMarker<TNewMarker>, new()
    {
        return ServiceCollection.NewBuilder<TNewMarker>();
    }
}
