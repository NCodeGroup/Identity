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

using System.Text.Json.Serialization;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a collection of <see cref="JsonConverter"/> instances for OpenID-related types.
/// </summary>
public interface IOpenIdJsonConverterProvider
{
    /// <summary>
    /// Gets a collection of <see cref="JsonConverter"/> instances for the specified OpenID environment.
    /// </summary>
    /// <param name="openIdEnvironment">The OpenID environment for which to retrieve the converters.</param>
    /// <returns>The collection of <see cref="JsonConverter"/> instances.</returns>
    IEnumerable<JsonConverter> GetJsonConverters(OpenIdEnvironment openIdEnvironment);
}
