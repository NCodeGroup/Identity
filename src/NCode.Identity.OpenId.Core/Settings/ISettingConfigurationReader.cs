// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Reads the settings carried by a configuration section, keyed by setting name, typing each one by its registered
/// <see cref="SettingDescriptor"/>. Setting names are a closed vocabulary: a child with no registered descriptor, that
/// does not apply within the scope whose settings the section carries, whose value does not convert to the
/// descriptor's type, or whose value is outside the setting's domain cannot be honored and is reported rather than
/// skipped.
/// </summary>
internal interface ISettingConfigurationReader
{
    /// <summary>
    /// Reports every child of <paramref name="section"/> that cannot be honored, each message naming the offending
    /// configuration path.
    /// </summary>
    /// <param name="section">The configuration section whose children are settings.</param>
    /// <param name="scopeName">The name of the registered scope whose settings the section carries.</param>
    /// <returns>The error messages, or an empty list when every setting can be honored.</returns>
    IReadOnlyList<string> Validate(IConfigurationSection section, string scopeName);

    /// <summary>
    /// Reads the settings carried by <paramref name="section"/>.
    /// </summary>
    /// <param name="section">The configuration section whose children are settings.</param>
    /// <param name="scopeName">The name of the registered scope whose settings the section carries.</param>
    /// <returns>The settings, one per child of the section.</returns>
    /// <exception cref="InvalidOperationException">A child cannot be honored; the message lists every such child as
    /// <see cref="Validate"/> reports it.</exception>
    IReadOnlyCollection<Setting> Read(IConfigurationSection section, string scopeName);
}
