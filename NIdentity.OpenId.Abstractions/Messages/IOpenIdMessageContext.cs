#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages;

/// <summary>
/// Contains supporting properties that an <see cref="IOpenIdMessage"/> requires.
/// </summary>
public interface IOpenIdMessageContext
{
    /// <summary>
    /// Gets the <see cref="HttpContext"/> that is associated with the current request.
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> to be used for any JSON serialization.
    /// </summary>
    JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// Attempts to get a <see cref="KnownParameter"/> that has the specified name.
    /// </summary>
    /// <param name="parameterName">The name of the <see cref="KnownParameter"/> to locate.</param>
    /// <param name="knownParameter">When this method returns, the <see cref="KnownParameter"/> with the specified
    /// name, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the <see cref="KnownParameter"/> with the specified name was found; otherwise,
    /// <c>false</c>.</returns>
    bool TryGetKnownParameter(string parameterName, [NotNullWhen(true)] out KnownParameter? knownParameter);
}
