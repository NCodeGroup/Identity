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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Endpoints;

/// <summary>
/// Declares the endpoints and child groups contained by an <see cref="IEndpointGroup"/>. An instance is scoped to a
/// single group and passed to its <see cref="IEndpointGroup.Build"/>, so every endpoint and child group it adds is
/// nested under that group.
/// </summary>
[PublicAPI]
public interface IEndpointGroupBuilder
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
