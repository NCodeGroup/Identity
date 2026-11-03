// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose.Secp256k1.KeyTypes;
using Xunit;

namespace NCode.Identity.Jose.Secp256k1.Tests;

/// <summary>
/// A fact that is skipped where the platform's cryptography does not support secp256k1, such as on macOS.
/// </summary>
public sealed class Secp256k1FactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Secp256k1FactAttribute"/> class.
    /// </summary>
    public Secp256k1FactAttribute()
    {
        if (!Secp256k1Curve.IsSupported)
        {
            Skip = "The platform's cryptography does not support the secp256k1 curve.";
        }
    }
}
