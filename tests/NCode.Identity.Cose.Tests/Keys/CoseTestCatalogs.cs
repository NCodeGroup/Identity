// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Keys;

namespace NCode.Identity.Cose.Tests.Keys;

/// <summary>
/// The registered COSE key types and curves the key tests compose their catalogs from.
/// </summary>
internal static class CoseTestCatalogs
{
    public static CoseKeyTypeDescriptor[] KeyTypes { get; } =
    [
        CoseKeyTypeDescriptors.Okp,
        CoseKeyTypeDescriptors.Ec2,
        CoseKeyTypeDescriptors.Rsa,
        CoseKeyTypeDescriptors.Symmetric,
        CoseKeyTypeDescriptors.Akp,
    ];

    public static CoseEllipticCurve[] Curves { get; } =
    [
        CoseEllipticCurves.P256,
        CoseEllipticCurves.P384,
        CoseEllipticCurves.P521,
        CoseEllipticCurves.X25519,
        CoseEllipticCurves.X448,
        CoseEllipticCurves.Ed25519,
        CoseEllipticCurves.Ed448,
        CoseEllipticCurves.Secp256k1,
    ];
}
