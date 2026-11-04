// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Contains the labels of the COSE_Key parameters that every key type shares (RFC 9052 §7.1).
/// </summary>
[PublicAPI]
public static class CoseKeyParameterLabels
{
    /// <summary>
    /// The <c>kty</c> parameter.
    /// </summary>
    public const int KeyType = 1;

    /// <summary>
    /// The <c>kid</c> parameter.
    /// </summary>
    public const int KeyId = 2;

    /// <summary>
    /// The <c>alg</c> parameter.
    /// </summary>
    public const int Algorithm = 3;

    /// <summary>
    /// The <c>key_ops</c> parameter.
    /// </summary>
    public const int KeyOperations = 4;

    /// <summary>
    /// The <c>Base IV</c> parameter.
    /// </summary>
    public const int BaseIv = 5;
}
