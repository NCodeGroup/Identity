#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
