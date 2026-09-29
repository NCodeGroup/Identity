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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Contributes baseline (off-the-shelf) values into the server's root <see cref="ISettingCollection"/>.
/// </summary>
/// <remarks>
/// Root settings are applied with replace/upsert semantics and form the operator ceiling that tenant and
/// client scopes narrow (via intersect) below it. Contributing a value here therefore <em>widens</em> what
/// downstream scopes may use, and leaving a <c>*_supported</c> collection unset means "no restriction" at
/// that level. Multiple providers are applied in registration order and may either replace a value or read
/// the in-progress collection to append to it. Values contributed here are still overridable by the
/// server's configuration section.
/// </remarks>
[PublicAPI]
public interface IDefaultSettingsProvider
{
    /// <summary>
    /// Contributes baseline settings into the provided <paramref name="settings"/> collection.
    /// </summary>
    /// <param name="settings">The root <see cref="ISettingCollection"/> being built.</param>
    void Configure(ISettingCollection settings);
}
