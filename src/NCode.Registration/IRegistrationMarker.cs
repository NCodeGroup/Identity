// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Registration;

/// <summary>
/// Marks a type as a registration marker: a zero-member, curiously-recurring type discriminator that
/// <see cref="IServiceBuilder{TMarker}"/> uses to identify a registered library at compile time.
/// </summary>
/// <typeparam name="TMarker">The self-referencing marker type.</typeparam>
[PublicAPI]
public interface IRegistrationMarker<TMarker>
    where TMarker : IRegistrationMarker<TMarker>, new()
{
    // A marker is a pure type discriminator; it carries no members.
}

#if false

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/>.
/// </summary>
[PublicAPI]
public readonly struct ExampleRegistrationMarker : IRegistrationMarker<ExampleRegistrationMarker>;

#endif
