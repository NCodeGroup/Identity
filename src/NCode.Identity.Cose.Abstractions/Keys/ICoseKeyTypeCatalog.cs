// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Provides the registered COSE_Key key types and elliptic curves.
/// </summary>
// See ADR-1409.
[PublicAPI]
public interface ICoseKeyTypeCatalog
{
    /// <summary>
    /// Gets the registered key types.
    /// </summary>
    IReadOnlyCollection<CoseKeyTypeDescriptor> KeyTypes { get; }

    /// <summary>
    /// Gets the registered curves.
    /// </summary>
    IReadOnlyCollection<CoseEllipticCurve> Curves { get; }

    /// <summary>
    /// Finds a key type by its IANA value.
    /// </summary>
    /// <param name="keyType">The IANA value of the key type.</param>
    /// <param name="descriptor">When this method returns <c>true</c>, the descriptor.</param>
    /// <returns><c>true</c> when the key type is registered; otherwise, <c>false</c>.</returns>
    bool TryGetKeyType(int keyType, [NotNullWhen(true)] out CoseKeyTypeDescriptor? descriptor);

    /// <summary>
    /// Finds a key type by the JSON Web Key type it maps onto.
    /// </summary>
    /// <param name="jwkKeyType">The JSON Web Key <c>kty</c> value.</param>
    /// <param name="descriptor">When this method returns <c>true</c>, the descriptor.</param>
    /// <returns><c>true</c> when a key type maps onto the value; otherwise, <c>false</c>.</returns>
    bool TryGetKeyTypeByJwkKeyType(
        string jwkKeyType,
        [NotNullWhen(true)] out CoseKeyTypeDescriptor? descriptor
    );

    /// <summary>
    /// Finds a curve of a key type by its IANA value.
    /// </summary>
    /// <param name="keyType">The IANA value of the key type.</param>
    /// <param name="identifier">The IANA value of the curve.</param>
    /// <param name="curve">When this method returns <c>true</c>, the curve.</param>
    /// <returns><c>true</c> when the curve is registered for the key type; otherwise, <c>false</c>.</returns>
    bool TryGetCurve(int keyType, int identifier, [NotNullWhen(true)] out CoseEllipticCurve? curve);

    /// <summary>
    /// Finds a curve of a key type by the JSON Web Key curve name it maps onto.
    /// </summary>
    /// <param name="keyType">The IANA value of the key type.</param>
    /// <param name="jwkCurveName">The JSON Web Key <c>crv</c> value.</param>
    /// <param name="curve">When this method returns <c>true</c>, the curve.</param>
    /// <returns><c>true</c> when a curve of the key type maps onto the name; otherwise, <c>false</c>.</returns>
    bool TryGetCurveByJwkName(
        int keyType,
        string jwkCurveName,
        [NotNullWhen(true)] out CoseEllipticCurve? curve
    );
}
