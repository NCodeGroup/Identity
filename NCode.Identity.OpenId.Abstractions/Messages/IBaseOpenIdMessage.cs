#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Represents the base interface for all <c>OAuth</c> or <c>OpenID Connect</c> messages.
/// </summary>
[PublicAPI]
public interface IBaseOpenIdMessage
{
    /// <summary>
    /// Gets the <see cref="OpenIdEnvironment"/> for the current instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    OpenIdEnvironment OpenIdEnvironment { get; }

    /// <summary>
    /// Gets the collection of strong-typed <c>OAuth</c> or <c>OpenID Connect</c> parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    IParameterCollection Parameters { get; }
}
