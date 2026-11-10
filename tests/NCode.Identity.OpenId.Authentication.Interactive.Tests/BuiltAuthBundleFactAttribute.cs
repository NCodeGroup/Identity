// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

/// <summary>
/// A fact that runs against the built <c>apps/auth</c> bundle, which the .NET gate never builds. The fact is skipped
/// unless the <see cref="IndexPathVariable"/> environment variable names the built bundle's <c>index.html</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BuiltAuthBundleFactAttribute : FactAttribute
{
    /// <summary>
    /// The name of the environment variable that holds the absolute path of the built bundle's <c>index.html</c>.
    /// </summary>
    public const string IndexPathVariable = "NCODE_AUTH_BUNDLE_INDEX";

    /// <summary>
    /// Gets the absolute path of the built bundle's <c>index.html</c>, or <c>null</c> when the environment names none.
    /// </summary>
    public static string? IndexPath => Environment.GetEnvironmentVariable(IndexPathVariable);

    /// <summary>
    /// Gets the reason a test against the built bundle is skipped, or <c>null</c> when the environment names the built bundle.
    /// </summary>
    internal static string? SkipReason =>
        string.IsNullOrEmpty(IndexPath)
            ? $"Set {IndexPathVariable} to the absolute path of the built apps/auth index.html to run this test; "
                + "the Frontend CI workflow sets it after it builds the bundle."
            : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltAuthBundleFactAttribute"/> class, which skips the fact when
    /// the environment names no built bundle.
    /// </summary>
    public BuiltAuthBundleFactAttribute()
    {
        Skip = SkipReason;
    }
}
