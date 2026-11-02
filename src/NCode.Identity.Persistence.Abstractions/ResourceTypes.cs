// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Persistence;

/// <summary>
/// Contains constants used to identify various resource types.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class ResourceTypes
{
    /// <summary>
    /// Contains the delimiter used to separate resource types.
    /// </summary>
    public const string Separator = "/";
}
