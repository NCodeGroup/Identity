// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Describes the COSE elliptic curves this library knows, each mapped onto a JSON Web Key curve name. The instances are
/// data: <c>AddCoseLibrary</c> registers them. Whether a key on a curve can be read depends on the registered secret key
/// types and on the platform.
/// </summary>
// See ADR-1409.
[PublicAPI]
public static class CoseEllipticCurves
{
    /// <summary>
    /// Gets the descriptor of the P-256 curve.
    /// </summary>
    public static CoseEllipticCurve P256 { get; } =
        new()
        {
            Identifier = CoseEllipticCurveIdentifiers.P256,
            KeyType = CoseKeyTypeIdentifiers.Ec2,
            JwkCurveName = "P-256",
        };

    /// <summary>
    /// Gets the descriptor of the P-384 curve.
    /// </summary>
    public static CoseEllipticCurve P384 { get; } =
        new()
        {
            Identifier = CoseEllipticCurveIdentifiers.P384,
            KeyType = CoseKeyTypeIdentifiers.Ec2,
            JwkCurveName = "P-384",
        };

    /// <summary>
    /// Gets the descriptor of the P-521 curve.
    /// </summary>
    public static CoseEllipticCurve P521 { get; } =
        new()
        {
            Identifier = CoseEllipticCurveIdentifiers.P521,
            KeyType = CoseKeyTypeIdentifiers.Ec2,
            JwkCurveName = "P-521",
        };

    /// <summary>
    /// Gets the descriptor of the X25519 curve.
    /// </summary>
    public static CoseEllipticCurve X25519 { get; } =
        new()
        {
            Identifier = CoseEllipticCurveIdentifiers.X25519,
            KeyType = CoseKeyTypeIdentifiers.Okp,
            JwkCurveName = "X25519",
        };

    /// <summary>
    /// Gets the descriptor of the X448 curve.
    /// </summary>
    public static CoseEllipticCurve X448 { get; } =
        new()
        {
            Identifier = CoseEllipticCurveIdentifiers.X448,
            KeyType = CoseKeyTypeIdentifiers.Okp,
            JwkCurveName = "X448",
        };

    /// <summary>
    /// Gets the descriptor of the Ed25519 curve.
    /// </summary>
    public static CoseEllipticCurve Ed25519 { get; } =
        new()
        {
            Identifier = CoseEllipticCurveIdentifiers.Ed25519,
            KeyType = CoseKeyTypeIdentifiers.Okp,
            JwkCurveName = "Ed25519",
        };

    /// <summary>
    /// Gets the descriptor of the Ed448 curve.
    /// </summary>
    public static CoseEllipticCurve Ed448 { get; } =
        new()
        {
            Identifier = CoseEllipticCurveIdentifiers.Ed448,
            KeyType = CoseKeyTypeIdentifiers.Okp,
            JwkCurveName = "Ed448",
        };

    /// <summary>
    /// Gets the descriptor of the secp256k1 curve.
    /// </summary>
    public static CoseEllipticCurve Secp256k1 { get; } =
        new()
        {
            Identifier = CoseEllipticCurveIdentifiers.Secp256k1,
            KeyType = CoseKeyTypeIdentifiers.Ec2,
            JwkCurveName = "secp256k1",
        };
}
