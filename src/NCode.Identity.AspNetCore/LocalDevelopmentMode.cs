// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.AspNetCore;

/// <summary>
/// Represents whether the local inner-loop relaxations are turned on, as the <c>LocalDevelopment</c> configuration
/// section says. Readers take this value instead of reading <see cref="LocalDevelopmentOptions"/>, so an options
/// validator that needs the switch reads no other options.
/// </summary>
/// <param name="enabled">
/// <c>true</c> when the relaxations are turned on; otherwise, <c>false</c>.
/// </param>
// See project-template ADR-0106.
[PublicAPI]
public sealed class LocalDevelopmentMode(bool enabled)
{
    /// <summary>
    /// Gets a value indicating whether the local inner-loop relaxations are turned on.
    /// </summary>
    public bool Enabled { get; } = enabled;
}
