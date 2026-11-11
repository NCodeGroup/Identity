// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Registration.AspNetCore;

/// <summary>
/// Declares the endpoints and child groups contained by an <see cref="IEndpointGroup"/>. It is itself an
/// <see cref="IServiceBuilder"/>, so a group registers the services its endpoints depend on — including builder-level
/// helpers such as message factories — alongside the <see cref="AddEndpoint{T}"/> calls that declare them. An instance
/// is scoped to a single group and passed to its <see cref="IEndpointGroup.ConfigureServices"/>, so every endpoint and
/// child group it adds is nested under that group.
/// </summary>
[PublicAPI]
public interface IEndpointGroupBuilder : IServiceBuilder
{
    /// <summary>
    /// Adds an <see cref="IEndpointProvider"/> endpoint to the current group.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IEndpointProvider"/> to add.</typeparam>
    void AddEndpoint<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
    >()
        where T : class, IEndpointProvider;

    /// <summary>
    /// Adds a child <see cref="IEndpointGroup"/> nested under the current group and recursively registers its contents.
    /// </summary>
    /// <typeparam name="T">The type of the child <see cref="IEndpointGroup"/> to add.</typeparam>
    void AddGroup<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
    >()
        where T : class, IEndpointGroup, new();
}
