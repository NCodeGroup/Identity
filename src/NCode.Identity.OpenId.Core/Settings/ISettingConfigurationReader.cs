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

using Microsoft.Extensions.Configuration;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Reads the settings carried by a configuration section, keyed by setting name, typing each one by its registered
/// <see cref="SettingDescriptor"/>. Setting names are a closed vocabulary: a child with no registered descriptor, whose
/// value does not convert to the descriptor's type, or whose value is outside the setting's domain cannot be honored and
/// is reported rather than skipped.
/// </summary>
internal interface ISettingConfigurationReader
{
    /// <summary>
    /// Reports every child of <paramref name="section"/> that cannot be honored, each message naming the offending
    /// configuration path.
    /// </summary>
    /// <param name="section">The configuration section whose children are settings.</param>
    /// <returns>The error messages, or an empty list when every setting can be honored.</returns>
    IReadOnlyList<string> Validate(IConfigurationSection section);

    /// <summary>
    /// Reads the settings carried by <paramref name="section"/>.
    /// </summary>
    /// <param name="section">The configuration section whose children are settings.</param>
    /// <returns>The settings, one per child of the section.</returns>
    /// <exception cref="InvalidOperationException">A child cannot be honored; the message lists every such child as
    /// <see cref="Validate"/> reports it.</exception>
    IReadOnlyCollection<Setting> Read(IConfigurationSection section);
}
