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

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides an abstraction for selecting the appropriate <see cref="IOpenIdMessageFactory"/> for a given type discriminator.
/// </summary>
public interface IOpenIdMessageFactorySelector
{
    /// <summary>
    /// Gets the message factory for the specified type discriminator.
    /// </summary>
    /// <param name="typeDiscriminator">The type discriminator of the message to create.</param>
    /// <returns>The message factory for the specified type discriminator.</returns>
    IOpenIdMessageFactory GetFactory(string typeDiscriminator);
}
