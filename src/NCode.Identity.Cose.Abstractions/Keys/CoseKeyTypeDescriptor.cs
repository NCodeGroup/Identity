// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Describes a COSE_Key key type: its IANA value, the JSON Web Key type it maps onto and its parameters. Descriptors
/// are registered data, so an application adds a key type by registering a descriptor.
/// </summary>
// See ADR-1409.
[PublicAPI]
public sealed class CoseKeyTypeDescriptor
{
    /// <summary>
    /// Gets the value of the key type in the IANA COSE Key Types registry.
    /// </summary>
    public required int KeyType { get; init; }

    /// <summary>
    /// Gets the JSON Web Key <c>kty</c> value the key type maps onto.
    /// </summary>
    public required string JwkKeyType { get; init; }

    /// <summary>
    /// Gets the parameters of the key type, apart from the ones every key type shares.
    /// </summary>
    public required ImmutableArray<CoseKeyParameter> Parameters { get; init; }

    /// <summary>
    /// Gets a value indicating whether a key of this type must carry its <c>alg</c> parameter, because the
    /// key bytes alone do not say which algorithm the key serves.
    /// </summary>
    public required bool RequiresAlgorithm { get; init; }
}
