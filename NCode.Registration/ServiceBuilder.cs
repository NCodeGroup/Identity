#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Registration;

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
    /// Factory method to create a new <see cref="IServiceBuilder{T}"/> instance with the specified type discriminator.
    /// </summary>
    /// <typeparam name="TMarker">The type that discriminates the service builder.</typeparam>
    IServiceBuilder<TMarker> New<TMarker>()
        where TMarker : IMarker<TMarker>;

    /// <summary>
    /// Factory method to create a new <see cref="IServiceBuilder{T}"/> instance and register a new registration marker with the specified type discriminator.
    /// </summary>
    /// <typeparam name="TMarker">The type that discriminates the service builder.</typeparam>
    IServiceBuilder<TMarker> Register<TMarker>()
        where TMarker : IRegistrationMarker<TMarker>;
}

/// <summary>
/// Provides the ability to configure services using the builder pattern with a type discriminator.
/// </summary>
/// <typeparam name="TMarker">The type that discriminates the service builder.</typeparam>
[PublicAPI]
public interface IServiceBuilder<TMarker> : IServiceBuilder
    where TMarker : IMarker<TMarker>
{
    // nothing
}

/// <summary>
/// Provides a default implementation of the <see cref="IServiceBuilder"/> abstraction.
/// </summary>
[PublicAPI]
public class ServiceBuilder : IServiceBuilder
{
    /// <summary>
    /// Creates a new instance of <see cref="IServiceBuilder{T}"/>.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> that is used to configure services.</param>
    /// <typeparam name="TMarker">The type that discriminates the library builder.</typeparam>
    public static IServiceBuilder<TMarker> Create<TMarker>(IServiceCollection serviceCollection)
        where TMarker : IMarker<TMarker>
    {
        return new ServiceBuilder<TMarker>
        {
            ServiceCollection = serviceCollection
        };
    }

    /// <summary>
    /// Creates a new instance of <see cref="IServiceBuilder{T}"/> and registers a registration marker with the specified type discriminator.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> that is used to configure services.</param>
    /// <typeparam name="TMarker">The type that discriminates the library builder.</typeparam>
    public static IServiceBuilder<TMarker> Register<TMarker>(IServiceCollection serviceCollection)
        where TMarker : IRegistrationMarker<TMarker>
    {
        serviceCollection.AddRegistrationMarker<TMarker>();
        return Create<TMarker>(serviceCollection);
    }

    /// <inheritdoc/>
    public required IServiceCollection ServiceCollection { get; init; }

    /// <inheritdoc/>
    public IServiceBuilder<TMarker> New<TMarker>()
        where TMarker : IMarker<TMarker>
    {
        return Create<TMarker>(ServiceCollection);
    }

    /// <inheritdoc/>
    public IServiceBuilder<TMarker> Register<TMarker>()
        where TMarker : IRegistrationMarker<TMarker>
    {
        ServiceCollection.AddRegistrationMarker<TMarker>();
        return New<TMarker>();
    }
}

/// <summary>
/// Provides a default implementation of the <see cref="IServiceBuilder{T}"/> abstraction.
/// </summary>
/// <typeparam name="TMarker">The type that discriminates the service builder.</typeparam>
public class ServiceBuilder<TMarker> : ServiceBuilder, IServiceBuilder<TMarker>
    where TMarker : IMarker<TMarker>
{
    // nothing
}
