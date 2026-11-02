// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.SharedState;

/// <summary>
/// Represents the counts that <see cref="IExpiringCounterSet"/> reports for one
/// <see cref="ExpiringCounterIncrement"/> of a batch it reached.
/// </summary>
/// <param name="Count">The count of the incremented counter after the increment, or zero when its expiry was not in
/// the future.</param>
/// <param name="ReferenceCount">The count of the reference counter, or zero when it was not read.</param>
// See ADR-1246.
[PublicAPI]
public readonly record struct ExpiringCounterCount(long Count, long ReferenceCount);
