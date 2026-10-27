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

namespace NCode.Identity.SharedState;

/// <summary>
/// Represents a value that <see cref="IExpiringValueSet"/> stores, together with the version a replacement must name.
/// </summary>
/// <param name="Value">The stored bytes.</param>
/// <param name="Version">The version of the stored value, which a replacement must name as its expected version. A value
/// starts at version one, and each replacement increments it.</param>
// See ADR-0267.
[PublicAPI]
public readonly record struct ExpiringValue(ReadOnlyMemory<byte> Value, long Version);
