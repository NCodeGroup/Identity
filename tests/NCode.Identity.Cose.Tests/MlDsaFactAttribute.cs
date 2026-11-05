// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Cose.Tests;

/// <summary>
/// A fact that runs only where the platform supports ML-DSA and is reported as skipped elsewhere.
/// </summary>
public sealed class MlDsaFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MlDsaFactAttribute"/> class.
    /// </summary>
    public MlDsaFactAttribute()
    {
        if (!MLDsa.IsSupported)
        {
            Skip = "The test needs a platform cryptography library that supports ML-DSA.";
        }
    }
}
