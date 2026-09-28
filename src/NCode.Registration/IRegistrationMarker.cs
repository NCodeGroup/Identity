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

namespace NCode.Registration;

/// <summary>
/// The base interface for all registration markers.
/// </summary>
/// <typeparam name="TMarker">The type that discriminates the marker interface.</typeparam>
[PublicAPI]
public interface IRegistrationMarker<TMarker> : IMarker<TMarker>
    where TMarker : IRegistrationMarker<TMarker>, new()
{
    /// <summary>
    /// Gets the display name of the registration marker.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the name of the method used to configure the services for this registration marker.
    /// </summary>
    string ConfigureMethod { get; }
}

#if false

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/>.
/// </summary>
[PublicAPI]
public readonly struct ExampleRegistrationMarker : IRegistrationMarker<ExampleRegistrationMarker>
{
    /// <inheritdoc />
    public string DisplayName => "Example Registration Marker";

    /// <inheritdoc />
    public string ConfigureMethod => "AddExampleServices";
}

#endif
