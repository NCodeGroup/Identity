// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Limits an endpoint to the BFF frontend it names. An endpoint with several of these attributes serves each named
/// frontend, and an endpoint with none serves every frontend.
/// </summary>
/// <param name="name">The name of the frontend that the endpoint serves.</param>
[PublicAPI]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class BffFrontendAttribute(string name) : Attribute
{
    /// <summary>
    /// Gets the name of the frontend that the endpoint serves.
    /// </summary>
    public string Name { get; } = RequireName(name);

    private static string RequireName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name;
    }
}
