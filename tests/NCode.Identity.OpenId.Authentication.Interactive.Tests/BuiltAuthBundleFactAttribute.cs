#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
    /// Initializes a new instance of the <see cref="BuiltAuthBundleFactAttribute"/> class, which skips the fact when
    /// the environment names no built bundle.
    /// </summary>
    public BuiltAuthBundleFactAttribute()
    {
        if (string.IsNullOrEmpty(IndexPath))
        {
            Skip =
                $"Set {IndexPathVariable} to the absolute path of the built apps/auth index.html to run this test; "
                + "the Frontend CI workflow sets it after it builds the bundle.";
        }
    }
}
