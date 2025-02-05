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

using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides an abstraction for creating <see cref="IOpenIdMessage"/> instances.
/// </summary>
public interface IOpenIdMessageFactory
{
    /// <summary>
    /// Gets the type discriminator for the message to create.
    /// </summary>
    string TypeDiscriminator { get; }

    /// <summary>
    /// Creates a new instance of the <see cref="IOpenIdMessage"/> with the specified parameters.
    /// </summary>
    /// <param name="parameters">The parameters to initialize the message with.</param>
    /// <returns>The new instance of the <see cref="IOpenIdMessage"/>.</returns>
    IOpenIdMessage Create(IEnumerable<IParameter> parameters);
}
