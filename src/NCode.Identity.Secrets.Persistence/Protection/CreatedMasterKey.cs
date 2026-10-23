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

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains a newly created master key in both of its forms: the row that is persisted, which holds the key only in
/// a wrapped form, and the unwrapped key that protects new values.
/// </summary>
/// <param name="Row">The row to persist, which holds the master key only in a wrapped form.</param>
/// <param name="Key">The active master key, which the caller owns and must dispose.</param>
// See ADR-0228.
internal readonly record struct CreatedMasterKey(PersistedMasterKey Row, MasterKey Key);
