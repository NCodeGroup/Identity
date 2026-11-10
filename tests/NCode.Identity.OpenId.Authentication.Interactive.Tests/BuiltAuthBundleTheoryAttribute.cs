// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

/// <summary>
/// A theory that runs against the built <c>apps/auth</c> bundle, which the .NET gate never builds. The theory is
/// skipped unless the <see cref="BuiltAuthBundleFactAttribute.IndexPathVariable"/> environment variable names the
/// built bundle's <c>index.html</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BuiltAuthBundleTheoryAttribute : TheoryAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltAuthBundleTheoryAttribute"/> class, which skips the theory
    /// when the environment names no built bundle.
    /// </summary>
    public BuiltAuthBundleTheoryAttribute()
    {
        Skip = BuiltAuthBundleFactAttribute.SkipReason;
    }
}
