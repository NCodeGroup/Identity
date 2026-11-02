// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Factory abstraction for creating <see cref="ISettingCollection"/> instances.
/// </summary>
[PublicAPI]
public interface ISettingCollectionFactory
{
    /// <summary>
    /// Creates a new instance of <see cref="ISettingCollection"/> that is empty.
    /// </summary>
    /// <returns>The new created <see cref="ISettingCollection"/> instance.</returns>
    ISettingCollection Create();

    /// <summary>
    /// Creates a new instance of <see cref="ISettingCollection"/> with the specified settings.
    /// </summary>
    /// <param name="settings">The collection of <see cref="Setting"/> instances to initialize the collection with.</param>
    /// <returns>The new created <see cref="ISettingCollection"/> instance.</returns>
    ISettingCollection Create(IEnumerable<Setting> settings);
}
