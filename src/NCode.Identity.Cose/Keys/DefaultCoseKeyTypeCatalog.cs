// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Provides the default implementation of <see cref="ICoseKeyTypeCatalog"/> over the registered key type and curve
/// descriptors, validated as a whole when the catalog is composed.
/// </summary>
// See ADR-1409.
internal class DefaultCoseKeyTypeCatalog : ICoseKeyTypeCatalog
{
    private FrozenDictionary<int, CoseKeyTypeDescriptor> KeyTypesByValue { get; }
    private FrozenDictionary<string, CoseKeyTypeDescriptor> KeyTypesByJwkKeyType { get; }
    private FrozenDictionary<
        (int KeyType, int Identifier),
        CoseEllipticCurve
    > CurvesByIdentifier { get; }
    private FrozenDictionary<(int KeyType, string Name), CoseEllipticCurve> CurvesByJwkName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCoseKeyTypeCatalog"/> class.
    /// </summary>
    /// <param name="keyTypes">The registered key types.</param>
    /// <param name="curves">The registered curves.</param>
    /// <exception cref="InvalidOperationException">An entry is <c>null</c>, names no JSON Web Key element, repeats a
    /// value, uses a label reserved for the common parameters, or a curve names an unregistered key type.</exception>
    public DefaultCoseKeyTypeCatalog(
        IEnumerable<CoseKeyTypeDescriptor> keyTypes,
        IEnumerable<CoseEllipticCurve> curves
    )
    {
        ArgumentNullException.ThrowIfNull(keyTypes);
        ArgumentNullException.ThrowIfNull(curves);

        var byValue = new Dictionary<int, CoseKeyTypeDescriptor>();
        var byJwk = new Dictionary<string, CoseKeyTypeDescriptor>(StringComparer.Ordinal);
        foreach (var keyType in keyTypes)
        {
            if (keyType is null)
                throw new InvalidOperationException("A registered COSE key type is null.");

            if (string.IsNullOrWhiteSpace(keyType.JwkKeyType))
                throw new InvalidOperationException(
                    $"The COSE key type {keyType.KeyType} names no JSON Web Key type."
                );

            if (keyType.Parameters.IsDefault)
                throw new InvalidOperationException(
                    $"The COSE key type {keyType.KeyType} has no parameter list."
                );

            if (!byValue.TryAdd(keyType.KeyType, keyType))
                throw new InvalidOperationException(
                    $"The COSE key type {keyType.KeyType} is registered more than once."
                );

            if (!byJwk.TryAdd(keyType.JwkKeyType, keyType))
                throw new InvalidOperationException(
                    $"The JSON Web Key type '{keyType.JwkKeyType}' is mapped by more than one COSE key type."
                );

            var labels = new HashSet<int>();
            foreach (var parameter in keyType.Parameters)
            {
                if (parameter is null || string.IsNullOrWhiteSpace(parameter.JwkMember))
                    throw new InvalidOperationException(
                        $"A parameter of the COSE key type {keyType.KeyType} names no JSON Web Key member."
                    );

                if (parameter.Label is >= 1 and <= 5)
                    throw new InvalidOperationException(
                        $"The parameter label {parameter.Label} of the COSE key type {keyType.KeyType} is reserved for the common parameters."
                    );

                if (!labels.Add(parameter.Label))
                    throw new InvalidOperationException(
                        $"The COSE key type {keyType.KeyType} repeats the parameter label {parameter.Label}."
                    );
            }
        }

        var curvesById = new Dictionary<(int, int), CoseEllipticCurve>();
        var curvesByName = new Dictionary<(int, string), CoseEllipticCurve>();
        foreach (var curve in curves)
        {
            if (curve is null)
                throw new InvalidOperationException("A registered COSE elliptic curve is null.");

            if (string.IsNullOrWhiteSpace(curve.JwkCurveName))
                throw new InvalidOperationException(
                    $"The COSE elliptic curve {curve.Identifier} names no JSON Web Key curve."
                );

            if (!byValue.ContainsKey(curve.KeyType))
                throw new InvalidOperationException(
                    $"The COSE elliptic curve {curve.Identifier} names the unregistered key type {curve.KeyType}."
                );

            if (!curvesById.TryAdd((curve.KeyType, curve.Identifier), curve))
                throw new InvalidOperationException(
                    $"The COSE elliptic curve {curve.Identifier} is registered more than once for the key type {curve.KeyType}."
                );

            if (!curvesByName.TryAdd((curve.KeyType, curve.JwkCurveName), curve))
                throw new InvalidOperationException(
                    $"The JSON Web Key curve '{curve.JwkCurveName}' is mapped by more than one COSE curve of the key type {curve.KeyType}."
                );
        }

        KeyTypesByValue = byValue.ToFrozenDictionary();
        KeyTypesByJwkKeyType = byJwk.ToFrozenDictionary(StringComparer.Ordinal);
        CurvesByIdentifier = curvesById.ToFrozenDictionary();
        CurvesByJwkName = curvesByName.ToFrozenDictionary();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<CoseKeyTypeDescriptor> KeyTypes => KeyTypesByValue.Values;

    /// <inheritdoc />
    public IReadOnlyCollection<CoseEllipticCurve> Curves => CurvesByIdentifier.Values;

    /// <inheritdoc />
    public bool TryGetKeyType(
        int keyType,
        [NotNullWhen(true)] out CoseKeyTypeDescriptor? descriptor
    ) => KeyTypesByValue.TryGetValue(keyType, out descriptor);

    /// <inheritdoc />
    public bool TryGetKeyTypeByJwkKeyType(
        string jwkKeyType,
        [NotNullWhen(true)] out CoseKeyTypeDescriptor? descriptor
    )
    {
        ArgumentNullException.ThrowIfNull(jwkKeyType);

        return KeyTypesByJwkKeyType.TryGetValue(jwkKeyType, out descriptor);
    }

    /// <inheritdoc />
    public bool TryGetCurve(
        int keyType,
        int identifier,
        [NotNullWhen(true)] out CoseEllipticCurve? curve
    ) => CurvesByIdentifier.TryGetValue((keyType, identifier), out curve);

    /// <inheritdoc />
    public bool TryGetCurveByJwkName(
        int keyType,
        string jwkCurveName,
        [NotNullWhen(true)] out CoseEllipticCurve? curve
    )
    {
        ArgumentNullException.ThrowIfNull(jwkCurveName);

        return CurvesByJwkName.TryGetValue((keyType, jwkCurveName), out curve);
    }
}
