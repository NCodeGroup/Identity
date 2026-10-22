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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Describes a value that a client registers and that an effective setting does not permit, as found by an
/// <see cref="IClientCoherenceRule"/>.
/// </summary>
/// <param name="SettingName">The name of the client setting that registers the value.</param>
/// <param name="Value">The registered value that is not permitted.</param>
/// <param name="CeilingName">The name of the effective setting that does not permit the value.</param>
// See ADR-0206.
[PublicAPI]
public readonly record struct ClientCoherenceViolation(
    string SettingName,
    string Value,
    string CeilingName
);
