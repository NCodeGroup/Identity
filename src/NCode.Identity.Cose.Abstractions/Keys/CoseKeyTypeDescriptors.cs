// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Jwk;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Describes the COSE_Key key types this library knows, each mapped onto a JSON Web Key type. The instances are data:
/// <c>AddCoseLibrary</c> registers them, and an application registers more.
/// </summary>
// See ADR-1409.
[PublicAPI]
public static class CoseKeyTypeDescriptors
{
    /// <summary>
    /// Gets the descriptor of the <c>OKP</c> key type.
    /// </summary>
    public static CoseKeyTypeDescriptor Okp { get; } =
        new()
        {
            KeyType = CoseKeyTypeIdentifiers.Okp,
            JwkKeyType = JsonWebKeyTypes.Okp,
            RequiresAlgorithm = false,
            Parameters =
            [
                new CoseKeyParameter
                {
                    Label = -1,
                    JwkMember = JsonWebKeyParameterNames.Curve,
                    Encoding = CoseKeyParameterEncoding.Curve,
                    IsPrivate = false,
                },
                new CoseKeyParameter
                {
                    Label = -2,
                    JwkMember = JsonWebKeyParameterNames.X,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = false,
                },
                new CoseKeyParameter
                {
                    Label = -4,
                    JwkMember = JsonWebKeyParameterNames.D,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
            ],
        };

    /// <summary>
    /// Gets the descriptor of the <c>EC2</c> key type.
    /// </summary>
    public static CoseKeyTypeDescriptor Ec2 { get; } =
        new()
        {
            KeyType = CoseKeyTypeIdentifiers.Ec2,
            JwkKeyType = JsonWebKeyTypes.Ec,
            RequiresAlgorithm = false,
            Parameters =
            [
                new CoseKeyParameter
                {
                    Label = -1,
                    JwkMember = JsonWebKeyParameterNames.Curve,
                    Encoding = CoseKeyParameterEncoding.Curve,
                    IsPrivate = false,
                },
                new CoseKeyParameter
                {
                    Label = -2,
                    JwkMember = JsonWebKeyParameterNames.X,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = false,
                },
                new CoseKeyParameter
                {
                    Label = -3,
                    JwkMember = JsonWebKeyParameterNames.Y,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = false,
                },
                new CoseKeyParameter
                {
                    Label = -4,
                    JwkMember = JsonWebKeyParameterNames.D,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
            ],
        };

    /// <summary>
    /// Gets the descriptor of the <c>RSA</c> key type.
    /// </summary>
    public static CoseKeyTypeDescriptor Rsa { get; } =
        new()
        {
            KeyType = CoseKeyTypeIdentifiers.Rsa,
            JwkKeyType = JsonWebKeyTypes.Rsa,
            RequiresAlgorithm = false,
            Parameters =
            [
                new CoseKeyParameter
                {
                    Label = -1,
                    JwkMember = JsonWebKeyParameterNames.Modulus,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = false,
                },
                new CoseKeyParameter
                {
                    Label = -2,
                    JwkMember = JsonWebKeyParameterNames.Exponent,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = false,
                },
                new CoseKeyParameter
                {
                    Label = -3,
                    JwkMember = JsonWebKeyParameterNames.D,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
                new CoseKeyParameter
                {
                    Label = -4,
                    JwkMember = JsonWebKeyParameterNames.FirstPrimeFactor,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
                new CoseKeyParameter
                {
                    Label = -5,
                    JwkMember = JsonWebKeyParameterNames.SecondPrimeFactor,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
                new CoseKeyParameter
                {
                    Label = -6,
                    JwkMember = JsonWebKeyParameterNames.FirstFactorCrtExponent,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
                new CoseKeyParameter
                {
                    Label = -7,
                    JwkMember = JsonWebKeyParameterNames.SecondFactorCrtExponent,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
                new CoseKeyParameter
                {
                    Label = -8,
                    JwkMember = JsonWebKeyParameterNames.FirstCrtCoefficient,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
                new CoseKeyParameter
                {
                    Label = -9,
                    JwkMember = JsonWebKeyParameterNames.OtherPrimesInfo,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
            ],
        };

    /// <summary>
    /// Gets the descriptor of the <c>Symmetric</c> key type.
    /// </summary>
    public static CoseKeyTypeDescriptor Symmetric { get; } =
        new()
        {
            KeyType = CoseKeyTypeIdentifiers.Symmetric,
            JwkKeyType = JsonWebKeyTypes.Oct,
            RequiresAlgorithm = false,
            Parameters =
            [
                new CoseKeyParameter
                {
                    Label = -1,
                    JwkMember = JsonWebKeyParameterNames.SymmetricKey,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
            ],
        };

    /// <summary>
    /// Gets the descriptor of the <c>AKP</c> key type.
    /// </summary>
    public static CoseKeyTypeDescriptor Akp { get; } =
        new()
        {
            KeyType = CoseKeyTypeIdentifiers.Akp,
            JwkKeyType = JsonWebKeyTypes.Akp,
            RequiresAlgorithm = true,
            Parameters =
            [
                new CoseKeyParameter
                {
                    Label = -1,
                    JwkMember = JsonWebKeyParameterNames.PublicKey,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = false,
                },
                new CoseKeyParameter
                {
                    Label = -2,
                    JwkMember = JsonWebKeyParameterNames.PrivateKey,
                    Encoding = CoseKeyParameterEncoding.ByteString,
                    IsPrivate = true,
                },
            ],
        };
}
