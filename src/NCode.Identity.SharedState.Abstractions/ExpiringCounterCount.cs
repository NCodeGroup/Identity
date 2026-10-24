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

namespace NCode.Identity.SharedState;

/// <summary>
/// Represents the counts that <see cref="IExpiringCounterSet"/> reports for one
/// <see cref="ExpiringCounterIncrement"/> of a batch it reached.
/// </summary>
/// <param name="Count">The count of the incremented counter after the increment, or zero when its expiry was not in
/// the future.</param>
/// <param name="ReferenceCount">The count of the reference counter, or zero when it was not read.</param>
// See ADR-0246.
[PublicAPI]
public readonly record struct ExpiringCounterCount(long Count, long ReferenceCount);
