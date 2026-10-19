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

using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Describes why a settings document cannot be honored: a setting whose name has no registered descriptor, a setting
/// whose value does not conform to its descriptor's type, or a document that is not a settings object at all.
/// </summary>
/// <param name="SettingName">The name of the offending setting, or an empty string when the error concerns the
/// document as a whole.</param>
/// <param name="Message">A description of the error that is safe to return to the caller.</param>
[PublicAPI]
public readonly record struct SettingError(string SettingName, string Message);
