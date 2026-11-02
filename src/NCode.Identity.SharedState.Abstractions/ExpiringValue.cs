// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.SharedState;

/// <summary>
/// Represents a value that <see cref="IExpiringValueSet"/> stores, together with the version a replacement must name.
/// </summary>
/// <param name="Value">The stored bytes.</param>
/// <param name="Version">The version of the stored value, which a replacement must name as its expected version. A value
/// starts at version one, and each replacement increments it.</param>
// See ADR-1267.
[PublicAPI]
public readonly record struct ExpiringValue(ReadOnlyMemory<byte> Value, long Version);
