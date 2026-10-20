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


using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Describes an algorithm a client registers for a JWT addressed to it that its effective settings do not permit.
/// </summary>
/// <param name="RegisteredKey">The setting that registers the algorithm, such as <c>id_token_signed_response_alg</c>.</param>
/// <param name="Value">The registered algorithm, or the default the setting implies when it is not registered.</param>
/// <param name="PermittedKey">The <c>*_values_supported</c> setting that excludes the algorithm.</param>
internal readonly record struct ClientResponseAlgorithmViolation(
    SettingKey<string> RegisteredKey,
    string Value,
    SettingKey<IReadOnlyCollection<string>> PermittedKey
);
