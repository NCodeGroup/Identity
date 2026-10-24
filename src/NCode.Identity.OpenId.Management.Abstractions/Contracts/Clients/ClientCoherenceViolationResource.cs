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

namespace NCode.Identity.OpenId.Management.Contracts.Clients;

/// <summary>
/// Represents one relation a client's settings break: a value the client registers that an effective setting does not
/// permit, or a value an effective setting requires that the client does not register.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ClientCoherenceViolationResource
{
    /// <summary>
    /// Gets the name of the client setting that registers the value or that is required.
    /// </summary>
    public required string SettingName { get; init; }

    /// <summary>
    /// Gets the registered value that is not permitted, or an empty string when a required value is missing.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the name of the effective setting that does not permit the value or that requires it.
    /// </summary>
    public required string GoverningSettingName { get; init; }

    /// <summary>
    /// Gets the description of the violation, which names the settings and the values involved.
    /// </summary>
    public required string Description { get; init; }
}
