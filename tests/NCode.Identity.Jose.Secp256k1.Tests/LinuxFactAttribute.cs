// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Jose.Secp256k1.Tests;

/// <summary>
/// A fact that runs only on Linux and is reported as skipped on every other platform.
/// </summary>
public sealed class LinuxFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LinuxFactAttribute"/> class.
    /// </summary>
    public LinuxFactAttribute()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip = "The test asserts a behavior of the platform's cryptography on Linux.";
        }
    }
}
