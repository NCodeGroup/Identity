// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

/// <summary>
/// A theory that runs against the built <c>apps/mgmt</c> bundle, which the .NET gate never builds. The theory is
/// skipped unless the <see cref="BuiltConsoleBundleFactAttribute.IndexPathVariable"/> environment variable names the
/// built bundle's <c>index.html</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BuiltConsoleBundleTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltConsoleBundleTheoryAttribute"/> class, which skips the theory
    /// when the environment names no built bundle.
    /// </summary>
    public BuiltConsoleBundleTheoryAttribute()
    {
        Skip = BuiltConsoleBundleFactAttribute.SkipReason;
    }
}
