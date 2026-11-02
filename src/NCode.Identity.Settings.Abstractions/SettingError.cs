// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Describes why a settings document cannot be honored: a setting whose name has no registered descriptor, a setting
/// that does not apply within the scope whose document carries it, a setting whose value does not conform to its
/// descriptor's type or falls outside its domain, or a document that is not a settings object at all.
/// </summary>
/// <param name="SettingName">The name of the offending setting, or an empty string when the error concerns the
/// document as a whole.</param>
/// <param name="Message">A description of the error that is safe to return to the caller.</param>
[PublicAPI]
public readonly record struct SettingError(string SettingName, string Message);
