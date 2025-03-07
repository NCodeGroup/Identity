#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the ability to serialize and deserialize settings from JSON.
/// </summary>
[PublicAPI]
public interface ISettingSerializer
{
    /// <summary>
    /// Deserializes a collection of <see cref="Setting"/> instances from JSON.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
    /// <param name="settingsJson">The JSON to deserialize into <see cref="Setting"/> instances.</param>
    /// <returns>The collection of <see cref="Setting"/> instances.</returns>
    IReadOnlyCollection<Setting> DeserializeSettings(
        OpenIdEnvironment openIdEnvironment,
        JsonElement settingsJson
    );
}
