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

namespace NCode.Identity.OpenId.Authentication.Consent;

/// <summary>
/// Describes a single scope as presented on an interactive consent surface: its raw value, a friendly description (when
/// one is defined on the owning resource server), and whether the subject has already granted it.
/// </summary>
/// <param name="Value">The raw scope value (for example, <c>profile</c>).</param>
/// <param name="Description">A human-readable description of what the scope grants, or <see langword="null"/> when none
/// is defined.</param>
/// <param name="IsGranted">Whether the subject has already granted this scope (so the consent surface can pre-select
/// it).</param>
[PublicAPI]
public sealed record ConsentScope(string Value, string? Description, bool IsGranted);
