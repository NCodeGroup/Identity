// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Describes a scope of the settings cascade: a kind of entity that carries a settings document, and the scope whose
/// effective settings cascade into its own. Scopes are an extensible vocabulary: a package that stores settings for an
/// entity of its own registers that entity's scope, beneath the scope its settings inherit from, through
/// <c>ICollectionDataSource&lt;SettingScopeDescriptor&gt;</c>.
/// </summary>
// See ADR-1149.
[PublicAPI]
public sealed class SettingScopeDescriptor
{
    /// <summary>
    /// Gets the name of the scope, which a <see cref="SettingDescriptor"/> lists in
    /// <see cref="SettingDescriptor.AppliesTo"/> and which names the scope of a settings document when it is read or
    /// validated.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the name of the scope whose effective settings cascade into this scope's, or <see langword="null"/> when this
    /// scope is a root of the cascade. A document at the parent scope may carry every setting that applies within this
    /// scope, because the parent's settings are merged into this scope's effective settings.
    /// </summary>
    public string? ParentName { get; init; }
}
