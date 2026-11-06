// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

/// <summary>
/// A fact that runs against the built <c>apps/mgmt</c> bundle, which the .NET gate never builds. The fact is skipped
/// unless the <see cref="IndexPathVariable"/> environment variable names the built bundle's <c>index.html</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BuiltConsoleBundleFactAttribute : FactAttribute
{
    /// <summary>
    /// The name of the environment variable that holds the absolute path of the built bundle's <c>index.html</c>.
    /// </summary>
    public const string IndexPathVariable = "NCODE_MGMT_BUNDLE_INDEX";

    /// <summary>
    /// Gets the absolute path of the built bundle's <c>index.html</c>, or <c>null</c> when the environment names none.
    /// </summary>
    public static string? IndexPath => Environment.GetEnvironmentVariable(IndexPathVariable);

    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltConsoleBundleFactAttribute"/> class, which skips the fact
    /// when the environment names no built bundle.
    /// </summary>
    public BuiltConsoleBundleFactAttribute()
    {
        if (string.IsNullOrEmpty(IndexPath))
        {
            Skip =
                $"Set {IndexPathVariable} to the absolute path of the built apps/mgmt index.html to run this test; "
                + "the Frontend CI workflow sets it after it builds the bundle.";
        }
    }
}
