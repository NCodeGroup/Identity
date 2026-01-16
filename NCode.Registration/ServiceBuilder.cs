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

    IServiceBuilder<TNewMarker> NewBuilder<TNewMarker>()
        where TNewMarker : IRegistrationMarker<TNewMarker>, new();
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
/// Provides a default implementation of the <see cref="IServiceBuilder{TMarker}"/> abstraction.
/// </summary>
/// <typeparam name="TMarker">The type that discriminates the service builder.</typeparam>
[PublicAPI]
public class ServiceBuilder<TMarker> : IServiceBuilder<TMarker>
    where TMarker : IRegistrationMarker<TMarker>, new()
{
    /// <inheritdoc/>
    public IServiceCollection ServiceCollection { get; }

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
