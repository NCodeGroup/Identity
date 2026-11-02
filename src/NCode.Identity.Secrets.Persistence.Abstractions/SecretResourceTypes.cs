// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains constants used to identify various resource types.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SecretResourceTypes
{
    /// <summary>
    /// Contains the value used to identify the resource type for a secret.
    /// </summary>
    public const string Secret = "secret";

    /// <summary>
    /// Contains the value used to identify the resource type for a collection of secrets.
    /// </summary>
    public const string Secrets = "secrets";
}
