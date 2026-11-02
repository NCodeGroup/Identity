// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Represents a strongly typed key in a <see cref="ISettingCollection"/>.
/// </summary>
/// <typeparam name="TValue">The type of the setting's value.</typeparam>
[PublicAPI]
public readonly record struct SettingKey<TValue>(string SettingName)
    where TValue : notnull
{
    /// <summary>
    /// Gets the type of the setting's value.
    /// </summary>
    public Type ValueType => typeof(TValue);
}
