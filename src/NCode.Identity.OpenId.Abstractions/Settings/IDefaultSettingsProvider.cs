// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Contributes baseline (off-the-shelf) values into the platform's root <see cref="ISettingCollection"/>.
/// </summary>
/// <remarks>
/// Root settings are applied with replace/upsert semantics and form the operator ceiling that tenant and
/// client scopes narrow (via intersect) below it. Contributing a value here therefore <em>widens</em> what
/// downstream scopes may use, and leaving a <c>*_supported</c> collection unset means "no restriction" at
/// that level. Multiple providers are applied in registration order and may either replace a value or read
/// the in-progress collection to append to it. Values contributed here are still overridable by the
/// platform's configuration section.
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
